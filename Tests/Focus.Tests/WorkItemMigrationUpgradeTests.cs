using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Focus.Tests;

// Fixture riêng: test hạ cấp DB về M1 rồi nâng lên M2, không ảnh hưởng class khác.
[Trait("Category", "Integration")]
public sealed class WorkItemMigrationUpgradeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Test-Password-123456";

    [Fact]
    public async Task Upgrade_from_m1_with_user_and_refresh_token_preserves_data()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        await db.Database.MigrateAsync("20260917040942_M1_Auth");
        db.ChangeTracker.Clear();

        using var client = fixture.Factory.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/auth/register",
            new { email, password = Password, displayName = "Migrator" })).StatusCode);
        var pair = await (await client.PostAsJsonAsync("/auth/login",
            new { email, password = Password }))
            .Content.ReadFromJsonAsync<Application.UserModule.Contracts.TokenPair>();
        Assert.NotNull(pair?.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pair!.AccessToken);

        var userBefore = await db.Users.AsNoTracking()
            .Where(x => x.Email == email)
            .Select(x => new { x.Id, x.Email, x.DisplayName, x.Seed, x.PasswordHash })
            .SingleAsync();
        var refreshBefore = await db.RefreshTokens.AsNoTracking()
            .Where(x => x.User.Email == email)
            .Select(x => new { x.TokenHash, x.ExpiresAt, x.RevokedAt })
            .ToListAsync();
        Assert.Single(refreshBefore);

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var userAfter = await db.Users.AsNoTracking()
            .Where(x => x.Email == email)
            .Select(x => new { x.Id, x.Email, x.DisplayName, x.Seed, x.PasswordHash })
            .SingleAsync();
        var refreshAfter = await db.RefreshTokens.AsNoTracking()
            .Where(x => x.User.Email == email)
            .Select(x => new { x.TokenHash, x.ExpiresAt, x.RevokedAt })
            .ToListAsync();
        Assert.Equal(userBefore, userAfter);
        Assert.Equal(refreshBefore, refreshAfter);

        using var created = await client.PostAsJsonAsync("/work-items",
            new { title = "After upgrade", type = "Coding", complexity = "S" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var workItemId = body.GetProperty("id").GetGuid();

        using var verifyScope = fixture.Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FocusDbContext>();
        Assert.Equal(userAfter.Id, (await verifyDb.WorkItems.AsNoTracking()
            .SingleAsync(x => x.Id == workItemId)).UserId);
    }
}
