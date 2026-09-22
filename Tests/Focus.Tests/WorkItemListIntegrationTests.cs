using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemListIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Users_cannot_read_or_count_each_other_items()
    {
        using var first = await AuthenticatedClientAsync();
        using var second = await AuthenticatedClientAsync();
        await CreateAsync(first, "A1", "Coding", "S");
        await CreateAsync(first, "A2", "Testing", "M");
        await CreateAsync(second, "B1", "Coding", "S");

        var a = await GetAsync(first, "/work-items");
        Assert.Equal(2, a.GetProperty("totalCount").GetInt32());
        Assert.Equal(["A2", "A1"], Titles(a));
        var b = await GetAsync(second, "/work-items");
        Assert.Equal(1, b.GetProperty("totalCount").GetInt32());
        Assert.Equal(["B1"], Titles(b));
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        using var client = await AuthenticatedClientAsync();
        await CreateAsync(client, "C1", "Coding", "S");
        await CreateAsync(client, "C2", "Testing", "M");
        await CreateAsync(client, "C3", "Coding", "M");

        Assert.Equal(2, (await GetAsync(client, "/work-items?type=Coding")).GetProperty("totalCount").GetInt32());
        var combo = await GetAsync(client, "/work-items?type=Coding&status=Open");
        Assert.Equal(2, combo.GetProperty("totalCount").GetInt32());
        var done = await GetAsync(client, "/work-items?status=Done");
        Assert.Equal(0, done.GetProperty("totalCount").GetInt32());
        Assert.Equal(20, combo.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Paging_is_stable_and_beyond_range_returns_empty()
    {
        using var client = await AuthenticatedClientAsync();
        for (var i = 1; i <= 5; i++) await CreateAsync(client, $"P{i}", "Coding", "S");

        var page1 = await GetAsync(client, "/work-items?page=1&pageSize=2");
        Assert.Equal(["P5", "P4"], Titles(page1));
        Assert.Equal(5, page1.GetProperty("totalCount").GetInt32());
        var page2 = await GetAsync(client, "/work-items?page=2&pageSize=2");
        Assert.Equal(["P3", "P2"], Titles(page2));
        var page3 = await GetAsync(client, "/work-items?page=3&pageSize=2");
        Assert.Equal(["P1"], Titles(page3));
        var beyond = await GetAsync(client, "/work-items?page=99&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, await StatusOf(client, "/work-items?page=99&pageSize=2"));
        Assert.Empty(Titles(beyond));
        Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Empty_list_returns_200_with_zero_total()
    {
        using var client = await AuthenticatedClientAsync();
        var body = await GetAsync(client, "/work-items");
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Empty(Titles(body));
    }

    [Theory]
    [InlineData("?page=2147483647&pageSize=20")]
    [InlineData("?page=1073741825&pageSize=4")]
    public async Task Huge_page_returns_empty_without_overflow(string query)
    {
        using var client = await AuthenticatedClientAsync();
        await CreateAsync(client, "O1", "Coding", "S");
        await CreateAsync(client, "O2", "Coding", "S");

        using var response = await client.GetAsync("/work-items" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Order_falls_back_to_id_when_timestamps_collide()
    {
        using var client = await AuthenticatedClientAsync();
        var ids = new List<Guid>();
        for (var i = 1; i <= 5; i++) ids.Add(await CreateIdAsync(client, $"T{i}"));

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
            var frozen = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
            await db.WorkItems.Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, frozen));
        }

        var body = await GetAsync(client, "/work-items?pageSize=20");
        Assert.Equal(ids.OrderByDescending(x => x).ToList(),
            body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList());
    }

    [Fact]
    public async Task Filters_and_default_cover_all_statuses()
    {
        using var client = await AuthenticatedClientAsync();
        var open = await CreateIdAsync(client, "StayOpen");
        var done = await CreateIdAsync(client, "ToDone");
        var archived = await CreateIdAsync(client, "ToArchived");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PatchAsJsonAsync($"/work-items/{done}/status", new { status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PatchAsJsonAsync($"/work-items/{archived}/status", new { status = "Archived" })).StatusCode);

        Assert.Equal(3, (await GetAsync(client, "/work-items")).GetProperty("totalCount").GetInt32());
        var doneBody = await GetAsync(client, "/work-items?status=Done");
        Assert.Equal(1, doneBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(new List<Guid> { done }, doneBody.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        var openBody = await GetAsync(client, "/work-items?status=Open");
        Assert.Equal(1, openBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(new List<Guid> { open }, openBody.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
    }

    [Theory]
    [InlineData("?status=Bogus")]
    [InlineData("?type=Bogus")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task Invalid_query_returns_400(string query)
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.GetAsync("/work-items" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task List_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/work-items");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static List<string> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("title").GetString()!).ToList();

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpStatusCode> StatusOf(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return response.StatusCode;
    }

    private static async Task CreateAsync(HttpClient client, string title, string type, string complexity)
    {
        using var response = await client.PostAsJsonAsync("/work-items",
            new { title, type, complexity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateIdAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/work-items",
            new { title, type = "Coding", complexity = "S" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        using (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Lister" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
