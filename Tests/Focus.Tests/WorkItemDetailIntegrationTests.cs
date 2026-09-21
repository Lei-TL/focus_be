using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemDetailIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Detail_returns_item_with_sorted_direct_links()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "Dep A");
        var b = await CreateIdAsync(client, "Dep B");
        var c = await CreateIdAsync(client, "Main");
        await AddLinkAsync(c, b);
        await AddLinkAsync(c, a);

        var body = await GetAsync(client, $"/work-items/{c}");
        Assert.Equal("Main", body.GetProperty("title").GetString());
        Assert.Equal("Open", body.GetProperty("status").GetString());
        var links = body.GetProperty("dependsOnWorkItemIds").EnumerateArray()
            .Select(x => x.GetGuid()).ToList();
        Assert.Equal(links.OrderBy(x => x).ToList(), links);
        Assert.Equal(new List<Guid> { a, b }.OrderBy(x => x).ToList(), links);
    }

    [Fact]
    public async Task Detail_without_links_returns_empty_list()
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Solo");
        var body = await GetAsync(client, $"/work-items/{id}");
        Assert.Empty(body.GetProperty("dependsOnWorkItemIds").EnumerateArray());
    }

    [Fact]
    public async Task Detail_of_missing_id_returns_404()
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.GetAsync($"/work-items/{Guid.CreateVersion7()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Detail_of_other_owner_item_returns_404_without_leak()
    {
        using var first = await AuthenticatedClientAsync();
        using var second = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(first, "Private");

        using var response = await second.GetAsync($"/work-items/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private", body);
    }

    [Fact]
    public async Task Detail_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/work-items/{Guid.CreateVersion7()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Detail_with_malformed_id_returns_400()
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.GetAsync("/work-items/not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> CreateIdAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/work-items",
            new { title, type = "Coding", complexity = "S" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task AddLinkAsync(Guid workItemId, Guid dependsOnId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        db.WorkItemLinks.Add(new Domain.Entities.WorkItemModule.WorkItemLink {
            WorkItemId = workItemId, DependsOnWorkItemId = dependsOnId
        });
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        using (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Reader" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
