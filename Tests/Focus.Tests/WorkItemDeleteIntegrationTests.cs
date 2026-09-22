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
public sealed class WorkItemDeleteIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Delete_removes_item_and_repeated_delete_returns_404()
    {
        using var client = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(client, "Gone");

        using (var deleted = await client.DeleteAsync($"/work-items/{id}"))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var gone = await client.GetAsync($"/work-items/{id}"))
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using (var again = await client.DeleteAsync($"/work-items/{id}"))
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_links_both_ways_and_keeps_neighbors()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var c = await CreateIdAsync(client, "C");
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{b}/dependencies", a));
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", c));

        using (var deleted = await client.DeleteAsync($"/work-items/{a}"))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var bDetail = await GetAsync(client, $"/work-items/{b}");
        Assert.Empty(bDetail.GetProperty("dependsOnWorkItemIds").EnumerateArray());
        var cDetail = await GetAsync(client, $"/work-items/{c}");
        Assert.Equal("C", cDetail.GetProperty("title").GetString());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        Assert.Equal(0, db.WorkItemLinks.Count(x => x.WorkItemId == a || x.DependsOnWorkItemId == a));
    }

    [Fact]
    public async Task Delete_foreign_item_returns_404_and_keeps_row()
    {
        using var first = await AuthenticatedClientAsync();
        using var second = await AuthenticatedClientAsync();
        var id = await CreateIdAsync(first, "Private");

        using (var response = await second.DeleteAsync($"/work-items/{id}"))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await GetAsync(first, $"/work-items/{id}");
        Assert.Equal("Private", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Concurrent_add_link_and_delete_item_leave_no_dangling_link()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var c = await CreateIdAsync(client, "C");

        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/work-items/{a}/dependencies", new { dependsOnWorkItemId = c }),
            client.DeleteAsync($"/work-items/{b}"));
        var postCode = results[0].StatusCode;
        var deleteCode = results[1].StatusCode;
        results[0].Dispose();
        results[1].Dispose();
        // B không liên quan cạnh A→C nên cả hai đều thành công mọi thứ tự.
        Assert.Equal(HttpStatusCode.Created, postCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        Assert.Equal(0, db.WorkItemLinks.Count(x => x.WorkItemId == b || x.DependsOnWorkItemId == b));
        Assert.Equal(1, db.WorkItemLinks.Count(x => x.WorkItemId == a && x.DependsOnWorkItemId == c));
    }

    [Fact]
    public async Task Concurrent_add_link_to_deleting_item_has_no_dangling_link_or_500()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");

        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/work-items/{a}/dependencies", new { dependsOnWorkItemId = b }),
            client.DeleteAsync($"/work-items/{a}"));
        var postCode = results[0].StatusCode;
        var deleteCode = results[1].StatusCode;
        results[0].Dispose();
        results[1].Dispose();
        Assert.Equal(HttpStatusCode.NoContent, deleteCode);
        Assert.True(postCode is HttpStatusCode.Created or HttpStatusCode.NotFound, postCode.ToString());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        Assert.Equal(0, db.WorkItemLinks.Count(x => x.WorkItemId == a || x.DependsOnWorkItemId == a));
        var survivor = await GetAsync(client, $"/work-items/{b}");
        Assert.Equal("B", survivor.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Delete_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.DeleteAsync($"/work-items/{Guid.CreateVersion7()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_rolls_back_links_when_item_delete_fails()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", b));

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION abort_work_item_delete() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'fault-injection'; END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER trg_abort_work_item_delete
            BEFORE DELETE ON work_items FOR EACH ROW EXECUTE FUNCTION abort_work_item_delete();
            """);
        try
        {
            // Dưới TestServer không có exception handler nên lỗi trigger propagate
            // về client thay vì thành 500 như Kestrel thật; assert throw + rollback.
            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                client.DeleteAsync($"/work-items/{a}"));
            Assert.Contains("fault-injection", error.InnerException?.Message);
            db.ChangeTracker.Clear();
            Assert.NotNull(await db.WorkItems.FindAsync(a));
            Assert.Equal(1, await db.WorkItemLinks.CountAsync(x => x.WorkItemId == a));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER IF EXISTS trg_abort_work_item_delete ON work_items; " +
                "DROP FUNCTION IF EXISTS abort_work_item_delete();");
        }
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpStatusCode> StatusOfPost(HttpClient client, string url, Guid dependsOn)
    {
        using var response = await client.PostAsJsonAsync(url, new { dependsOnWorkItemId = dependsOn });
        return response.StatusCode;
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
            new { email, password = Password, displayName = "Deleter" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
