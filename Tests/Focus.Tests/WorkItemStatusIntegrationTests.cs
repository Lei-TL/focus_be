using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemStatusIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Status_cycle_sets_keeps_and_clears_closedAt()
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Cycle");

        var done1 = await PatchAsync(client, id, "Done");
        var closed1 = done1.GetProperty("closedAt").GetDateTimeOffset();
        Assert.Equal("Done", done1.GetProperty("status").GetString());
        Assert.True(closed1 <= DateTimeOffset.UtcNow);

        var done2 = await PatchAsync(client, id, "Done");
        // Giữ nguyên thời điểm đóng: cho phép lệch precision microsecond giữa
        // giá trị in-memory lần PATCH đầu và giá trị đọc về từ timestamptz.
        // Viết lại thật sẽ lệch hàng trăm ms (request riêng biệt).
        Assert.True((done2.GetProperty("closedAt").GetDateTimeOffset() - closed1).Duration()
            < TimeSpan.FromMilliseconds(1));

        var archived = await PatchAsync(client, id, "Archived");
        Assert.Equal("Archived", archived.GetProperty("status").GetString());
        Assert.True((archived.GetProperty("closedAt").GetDateTimeOffset() - closed1).Duration()
            < TimeSpan.FromMilliseconds(1));

        var open = await PatchAsync(client, id, "Open");
        Assert.Equal("Open", open.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, open.GetProperty("closedAt").ValueKind);
    }

    [Theory]
    [InlineData("""{"status":"Bogus"}""")]
    [InlineData("""{}""")]
    public async Task Status_rejects_invalid_input(string json)
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Target");
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PatchAsync($"/work-items/{id}/status", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Status_other_owner_item_returns_404_and_changes_nothing()
    {
        using var first = await AuthenticatedClientAsync();
        using var second = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(first, "Private");

        using var response = await second.PatchAsJsonAsync($"/work-items/{id}/status",
            new { status = "Done" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await GetAsync(first, $"/work-items/{id}");
        Assert.Equal("Open", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Status_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PatchAsJsonAsync(
            $"/work-items/{Guid.CreateVersion7()}/status", new { status = "Done" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> PatchAsync(HttpClient client, Guid id, string status)
    {
        using var response = await client.PatchAsJsonAsync($"/work-items/{id}/status",
            new { status });
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

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        using (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Switcher" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
