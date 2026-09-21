using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemUpdateIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Update_replaces_fields_and_bumps_updatedAt()
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Before", "Coding", "S");
        var before = await GetAsync(client, $"/work-items/{id}");

        using var response = await client.PutAsJsonAsync($"/work-items/{id}", new
        {
            title = "  After  ",
            description = "New desc",
            type = "Design",
            complexity = "L",
            deadline = DateTimeOffset.UtcNow.AddDays(5),
            userEstimateMinutes = 60
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("After", body.GetProperty("title").GetString());
        Assert.Equal("Design", body.GetProperty("type").GetString());
        Assert.Equal("L", body.GetProperty("complexity").GetString());
        Assert.Equal(60, body.GetProperty("userEstimateMinutes").GetInt32());
        Assert.True(body.GetProperty("updatedAt").GetDateTimeOffset()
            >= before.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Update_clears_nullable_fields_and_allows_past_deadline()
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Full", "Coding", "S");

        using var response = await client.PutAsJsonAsync($"/work-items/{id}", new
        {
            title = "Cleared",
            description = (string?)null,
            type = "Coding",
            complexity = "S",
            deadline = DateTimeOffset.UtcNow.AddDays(-3),
            userEstimateMinutes = (int?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("userEstimateMinutes").ValueKind);
        Assert.True(body.GetProperty("deadline").GetDateTimeOffset() < DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("""{"type":"Coding","complexity":"S"}""")]
    [InlineData("""{"title":"T","type":"Cooking","complexity":"S"}""")]
    [InlineData("""{"title":"T","type":"Coding","complexity":"S","userEstimateMinutes":0}""")]
    public async Task Update_rejects_invalid_input(string json)
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Target", "Coding", "S");
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync($"/work-items/{id}", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_other_owner_item_returns_404_and_changes_nothing()
    {
        using var first = await AuthenticatedClientAsync();
        using var second = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(first, "Private", "Coding", "S");

        using var response = await second.PutAsJsonAsync($"/work-items/{id}", new
        {
            title = "Hacked", type = "Design", complexity = "M"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        Assert.Equal("Private", (await db.WorkItems.FindAsync(id))!.Title);
    }

    [Fact]
    public async Task Update_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PutAsJsonAsync($"/work-items/{Guid.CreateVersion7()}", new
        {
            title = "No auth", type = "Coding", complexity = "S"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> CreateIdAsync(HttpClient client, string title, string type, string complexity)
    {
        using var response = await client.PostAsJsonAsync("/work-items",
            new { title, type, complexity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        using (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Updater" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
