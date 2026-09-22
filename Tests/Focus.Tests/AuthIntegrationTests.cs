using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Application.UserModule.Contracts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Focus.Tests;

[Trait("Category", "Integration")]
public sealed class AuthIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Failed_successor_insert_rolls_back_refresh_consumption()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        var user = new Domain.Entities.UserModule.User {
            Email = Guid.NewGuid().ToString("N") + "@example.test", DisplayName = "Rollback", PasswordHash = "test-only"
        };
        var old = new Domain.Entities.UserModule.RefreshToken {
            UserId = user.Id, TokenHash = new string('b', 64), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(old);
        await db.SaveChangesAsync();
        var collision = new Domain.Entities.UserModule.RefreshToken {
            UserId = user.Id, TokenHash = old.TokenHash, ExpiresAt = old.ExpiresAt
        };
        var store = new Infrastructure.UserModule.Persistence.UserAuthStore(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.TryRotateAsync(old.TokenHash, collision, DateTimeOffset.UtcNow, default));
        db.ChangeTracker.Clear();
        Assert.Null((await db.RefreshTokens.SingleAsync(x => x.Id == old.Id)).RevokedAt);
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task Auth_contract_rotation_and_persistence_work_on_isolated_postgres()
    {
        using var client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
        var invalid = await client.PostAsJsonAsync("/auth/register", new { email = "bad", password = "x", displayName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidJson = await invalid.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(invalidJson.TryGetProperty("errors", out _));

        var email = Guid.NewGuid().ToString("N") + "@example.test";
        const string password = "Test-Password-123456";
        var register = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password, displayName = "Test", role = "Admin" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal("/auth/me", register.Headers.Location!.ToString());
        var profile = (await register.Content.ReadFromJsonAsync<UserProfile>())!;
        Assert.Equal(email, profile.Email);
        Assert.Equal("User", profile.Role);
        Assert.Equal(50, profile.DefaultSessionMinutes);
        Assert.Equal("Asia/Ho_Chi_Minh", profile.TimeZoneId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", new { email, password = "wrong" })).StatusCode);
        var login = await client.PostAsJsonAsync("/auth/login", new { email = email.ToUpperInvariant(), password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        var me = await client.GetFromJsonAsync<UserProfile>("/auth/me");
        Assert.Equal(profile, me);
        Assert.InRange((pair.AccessExpiresAt - DateTimeOffset.UtcNow).TotalMinutes, 29, 30);
        Assert.InRange((pair.RefreshExpiresAt - DateTimeOffset.UtcNow).TotalDays, 29, 30);

        var refreshes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/auth/refresh", new { refreshToken = pair.RefreshToken })));
        Assert.Single(refreshes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(refreshes, x => x.StatusCode == HttpStatusCode.Unauthorized);
        var successor = (await refreshes.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<TokenPair>())!;
        Assert.NotEqual(pair.RefreshToken, successor.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = pair.RefreshToken })).StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == profile.Id);
        Assert.NotEqual(password, user.PasswordHash);
        var records = await db.RefreshTokens.Where(x => x.UserId == user.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, x => Assert.Equal(64, x.TokenHash.Length));
        Assert.DoesNotContain(records, x => x.TokenHash == successor.RefreshToken);
        Assert.Single(records, x => x.RevokedAt is not null);
        await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = successor.RefreshToken })).StatusCode);

        foreach (var (expiry, audience, signingKey) in new[] {
            (DateTime.UtcNow.AddMinutes(-1), "Focus.App", fixture.Key),
            (DateTime.UtcNow.AddMinutes(1), "wrong", fixture.Key),
            (DateTime.UtcNow.AddMinutes(1), "Focus.App", new string('x', 48)) })
        {
            var jwt = new JwtSecurityToken("Focus", audience, [new Claim("sub", user.Id.ToString())],
                DateTime.UtcNow.AddHours(-1), expiry,
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
        }
        await db.Database.MigrateAsync();
        Assert.Equal(profile.Seed, (await db.Users.SingleAsync(x => x.Id == profile.Id)).Seed);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
    }
}
