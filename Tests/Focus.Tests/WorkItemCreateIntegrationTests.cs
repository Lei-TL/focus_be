using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class WorkItemCreateIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Create_returns_201_with_defaults_and_location()
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.PostAsJsonAsync("/work-items", new
        {
            title = "  Build API  ",
            description = "Do the thing",
            type = "Coding",
            complexity = "M",
            deadline = DateTimeOffset.UtcNow.AddDays(2),
            userEstimateMinutes = 25
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Build API", body.GetProperty("title").GetString());
        Assert.Equal("Coding", body.GetProperty("type").GetString());
        Assert.Equal("M", body.GetProperty("complexity").GetString());
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("closedAt").ValueKind);
        Assert.Equal(25, body.GetProperty("userEstimateMinutes").GetInt32());
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/work-items/{id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_with_explicit_null_title_returns_400_and_creates_nothing()
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.PostAsJsonAsync("/work-items", new
        {
            title = (string?)null,
            type = "Coding",
            complexity = "S"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));

        using var list = await client.GetAsync("/work-items");
        var items = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, items.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Create_requires_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/work-items", new
        {
            title = "No auth", type = "Coding", complexity = "S"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_rejects_invalid_input(string name, string json)
    {
        using var client = await AuthenticatedClientAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/work-items", content);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, name);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _), name);
    }

    public static TheoryData<string, string> InvalidBodies()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        return new TheoryData<string, string>
        {
            { "blank title", """{"title":"   ","type":"Coding","complexity":"S"}""" },
            { "missing title", """{"type":"Coding","complexity":"S"}""" },
            { "title too long", $$"""{"title":"{{new string('t', 201)}}","type":"Coding","complexity":"S"}""" },
            { "missing type", """{"title":"T","complexity":"S"}""" },
            { "unknown type", """{"title":"T","type":"Cooking","complexity":"S"}""" },
            { "numeric type", """{"title":"T","type":2,"complexity":"S"}""" },
            { "unknown complexity", """{"title":"T","type":"Coding","complexity":"XXL"}""" },
            { "zero estimate", """{"title":"T","type":"Coding","complexity":"S","userEstimateMinutes":0}""" },
            { "negative estimate", """{"title":"T","type":"Coding","complexity":"S","userEstimateMinutes":-5}""" },
            { "past deadline", $$"""{"title":"T","type":"Coding","complexity":"S","deadline":"{{past}}"}""" }
        };
    }

    [Fact]
    public async Task Create_ignores_smuggled_owner_and_status()
    {
        var (client, ownerId) = await AuthenticatedClientWithIdAsync();
        using (client)
        using (var response = await client.PostAsJsonAsync("/work-items", new
        {
            title = "Smuggle",
            type = "Design",
            complexity = "S",
            userId = Guid.NewGuid(),
            status = "Done",
            closedAt = DateTimeOffset.UtcNow
        }))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        var row = await db.WorkItems.AsNoTracking().SingleAsync(x => x.Title == "Smuggle");
        Assert.Equal(ownerId, row.UserId);
        Assert.Equal(Domain.Enums.WorkItemStatus.Open, row.Status);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public async Task Link_table_enforces_pair_unique_and_no_self_reference()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        var user = new Domain.Entities.UserModule.User {
            Email = Guid.NewGuid().ToString("N") + "@example.test",
            DisplayName = "Links", PasswordHash = "test-only"
        };
        db.Users.Add(user);
        var first = new Domain.Entities.WorkItemModule.WorkItem { UserId = user.Id, Title = "A" };
        var second = new Domain.Entities.WorkItemModule.WorkItem { UserId = user.Id, Title = "B" };
        db.WorkItems.AddRange(first, second);
        await db.SaveChangesAsync();
        db.WorkItemLinks.Add(new Domain.Entities.WorkItemModule.WorkItemLink {
            WorkItemId = first.Id, DependsOnWorkItemId = second.Id
        });
        await db.SaveChangesAsync();

        db.WorkItemLinks.Add(new Domain.Entities.WorkItemModule.WorkItemLink {
            WorkItemId = first.Id, DependsOnWorkItemId = second.Id
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.WorkItemLinks.Add(new Domain.Entities.WorkItemModule.WorkItemLink {
            WorkItemId = first.Id, DependsOnWorkItemId = first.Id
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Migration_chain_preserves_m1_data_and_is_repeatable()
    {
        using var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/auth/register",
                new { email, password = Password, displayName = "Chain" })).StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        await db.Database.MigrateAsync();
        Assert.True(await db.Users.AnyAsync(x => x.Email == email));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var (client, _) = await AuthenticatedClientWithIdAsync();
        return client;
    }

    private async Task<(HttpClient Client, Guid OwnerId)> AuthenticatedClientWithIdAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var profile = await (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Worker" }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.UserProfile>();
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", pair!.AccessToken);
        return (client, profile!.Id);
    }
}
