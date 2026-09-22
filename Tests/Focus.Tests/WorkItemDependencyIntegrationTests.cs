using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemDependencyIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Add_builds_chain_and_rejects_closing_cycle()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var c = await CreateIdAsync(client, "C");

        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", b));
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{b}/dependencies", c));
        var detail = await GetAsync(client, $"/work-items/{a}");
        Assert.Equal([b], detail.GetProperty("dependsOnWorkItemIds").EnumerateArray().Select(x => x.GetGuid()));

        using var cycle = await client.PostAsJsonAsync($"/work-items/{c}/dependencies",
            new { dependsOnWorkItemId = a });
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
    }

    [Fact]
    public async Task Add_rejects_self_loop_duplicate_and_foreign_items()
    {
        using var client = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var foreign = await CreateIdAsync(other, "Foreign");

        using (var self = await client.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = a }))
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", b));
        using (var dup = await client.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = b }))
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        using (var missing = await client.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = Guid.CreateVersion7() }))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var foreignLink = await client.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = foreign }))
            Assert.Equal(HttpStatusCode.NotFound, foreignLink.StatusCode);
        using (var foreignRoot = await other.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = b }))
            Assert.Equal(HttpStatusCode.NotFound, foreignRoot.StatusCode);
        using (var empty = await client.PostAsJsonAsync($"/work-items/{a}/dependencies",
            new { dependsOnWorkItemId = Guid.Empty }))
            Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
    }

    [Fact]
    public async Task Concurrent_opposite_edges_allow_exactly_one()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");

        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/work-items/{a}/dependencies", new { dependsOnWorkItemId = b }),
            client.PostAsJsonAsync($"/work-items/{b}/dependencies", new { dependsOnWorkItemId = a }));
        var codes = results.Select(r => { using (r) return r.StatusCode; }).ToList();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.BadRequest, codes);
        Assert.Equal(2, codes.Count);
    }

    [Fact]
    public async Task Concurrent_duplicate_edge_creates_single_link()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync($"/work-items/{a}/dependencies", new { dependsOnWorkItemId = b })));
        var codes = results.Select(r => { using (r) return r.StatusCode; }).ToList();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);
    }

    [Fact]
    public async Task Add_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/work-items/{Guid.CreateVersion7()}/dependencies",
            new { dependsOnWorkItemId = Guid.CreateVersion7() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Remove_deletes_directed_edge_and_keeps_items_and_other_edges()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var c = await CreateIdAsync(client, "C");
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", b));
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{a}/dependencies", c));
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{b}/dependencies", c));

        using (var removed = await client.DeleteAsync($"/work-items/{a}/dependencies/{b}"))
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var detail = await GetAsync(client, $"/work-items/{a}");
        Assert.Equal([c], detail.GetProperty("dependsOnWorkItemIds").EnumerateArray().Select(x => x.GetGuid()));
        var bDetail = await GetAsync(client, $"/work-items/{b}");
        Assert.Equal([c], bDetail.GetProperty("dependsOnWorkItemIds").EnumerateArray().Select(x => x.GetGuid()));
    }

    [Fact]
    public async Task Remove_is_idempotent_and_keeps_reverse_edge()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        Assert.Equal(HttpStatusCode.Created, await StatusOfPost(client, $"/work-items/{b}/dependencies", a));

        using (var first = await client.DeleteAsync($"/work-items/{a}/dependencies/{b}"))
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using (var second = await client.DeleteAsync($"/work-items/{a}/dependencies/{b}"))
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var bDetail = await GetAsync(client, $"/work-items/{b}");
        Assert.Equal([a], bDetail.GetProperty("dependsOnWorkItemIds").EnumerateArray().Select(x => x.GetGuid()));
    }

    [Fact]
    public async Task Remove_with_missing_or_foreign_item_returns_404()
    {
        using var client = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");
        var foreign = await CreateIdAsync(other, "Foreign");

        using (var missing = await client.DeleteAsync($"/work-items/{a}/dependencies/{Guid.CreateVersion7()}"))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var foreignDep = await client.DeleteAsync($"/work-items/{a}/dependencies/{foreign}"))
            Assert.Equal(HttpStatusCode.NotFound, foreignDep.StatusCode);
        using (var foreignRoot = await other.DeleteAsync($"/work-items/{a}/dependencies/{b}"))
            Assert.Equal(HttpStatusCode.NotFound, foreignRoot.StatusCode);
    }

    [Fact]
    public async Task Concurrent_add_and_remove_keep_graph_consistent()
    {
        using var client = await AuthenticatedClientAsync();
        var a = await CreateIdAsync(client, "A");
        var b = await CreateIdAsync(client, "B");

        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/work-items/{a}/dependencies", new { dependsOnWorkItemId = b }),
            client.DeleteAsync($"/work-items/{a}/dependencies/{b}"));
        var postCode = results[0].StatusCode;
        var deleteCode = results[1].StatusCode;
        results[0].Dispose();
        results[1].Dispose();
        Assert.Equal(HttpStatusCode.Created, postCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteCode);

        var detail = await GetAsync(client, $"/work-items/{a}");
        var links = detail.GetProperty("dependsOnWorkItemIds").EnumerateArray().ToList();
        Assert.True(links.Count is 0 or 1);
        if (links.Count == 1) Assert.Equal(b, links[0].GetGuid());
    }

    [Fact]
    public async Task Remove_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.DeleteAsync(
            $"/work-items/{Guid.CreateVersion7()}/dependencies/{Guid.CreateVersion7()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
            new { email, password = Password, displayName = "Linker" })) { }
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return client;
    }
}
