using Application.UserModule.Abstractions;
using Application.UserModule.Contracts;
using Application.UserModule.Services;
using Domain.Entities.UserModule;
using Xunit;
namespace Focus.Tests;

public sealed class AuthApplicationTests
{
    private readonly FakeStore store = new();
    private readonly FakePasswords passwords = new();
    private readonly FakeIssuer tokens = new();
    private AuthApplicationService Service => new(store, passwords, tokens, new FakeSeed(), new FixedClock());

    [Fact]
    public async Task Register_normalizes_profile_and_assigns_stable_seed()
    {
        var profile = await Service.RegisterAsync(new RegisterUser("  TEST@example.test ", "password", " Name ", 50, "Asia/Ho_Chi_Minh"), default);
        Assert.NotNull(profile);
        Assert.Equal("test@example.test", profile.Email);
        Assert.Equal("Name", profile.DisplayName);
        Assert.Equal(42, profile.Seed);
        Assert.Equal("User", profile.Role);
        Assert.Equal("hashed:password", store.User!.PasswordHash);
    }

    [Fact]
    public async Task Duplicate_registration_race_returns_conflict_result()
    {
        store.CreateSucceeds = false;
        Assert.Null(await Service.RegisterAsync(new RegisterUser("test@example.test", "password", "Name", 50, "UTC"), default));
    }

    [Theory]
    [InlineData(false, PasswordCheck.Valid)]
    [InlineData(true, PasswordCheck.Failed)]
    public async Task Invalid_login_does_not_issue_or_persist_tokens(bool exists, PasswordCheck check)
    {
        store.User = exists ? new User() : null;
        passwords.Check = check;
        Assert.Null(await Service.LoginAsync("test@example.test", "bad", default));
        Assert.Equal(0, tokens.Issued);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Login_rehashes_when_required_and_preserves_seed()
    {
        store.User = new User { Seed = 91 };
        passwords.Check = PasswordCheck.RehashNeeded;
        var result = await Service.LoginAsync("USER@EXAMPLE.TEST", "new", default);
        Assert.NotNull(result);
        Assert.Equal("user@example.test", store.LookedUpEmail);
        Assert.Equal("hashed:new", store.User.PasswordHash);
        Assert.Equal(91, store.User.Seed);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public async Task Refresh_without_matching_user_does_not_issue_tokens()
    {
        Assert.Null(await Service.RefreshAsync("unknown", default));
        Assert.Equal(0, tokens.Issued);
    }

    [Fact]
    public async Task Refresh_losing_atomic_rotation_does_not_return_tokens()
    {
        store.User = new User();
        store.RotateSucceeds = false;
        Assert.Null(await Service.RefreshAsync("old", default));
    }

    [Fact]
    public async Task Refresh_success_returns_successor_and_uses_injected_clock()
    {
        store.User = new User();
        Assert.NotNull(await Service.RefreshAsync("old", default));
        Assert.Equal(FixedClock.Now, store.RotationTime);
        Assert.Equal("hash:old", store.OldHash);
    }

    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeSeed : ISeedGenerator { public long Create() => 42; }
    private sealed class FakePasswords : IPasswordService
    {
        public PasswordCheck Check { get; set; } = PasswordCheck.Valid;
        public string Hash(User user, string password) => "hashed:" + password;
        public PasswordCheck Verify(User user, string password) => Check;
    }
    private sealed class FakeIssuer : ITokenIssuer
    {
        public int Issued { get; private set; }
        public string HashRefreshToken(string rawToken) => "hash:" + rawToken;
        public IssuedTokens Issue(User user, DateTimeOffset now)
        {
            Issued++;
            return new(new TokenPair("access", "refresh", now.AddMinutes(30), now.AddDays(30)),
                new RefreshToken { UserId = user.Id, TokenHash = "hash:refresh", ExpiresAt = now.AddDays(30) });
        }
    }
    private sealed class FakeStore : IUserAuthStore
    {
        public User? User;
        public bool CreateSucceeds = true;
        public bool RotateSucceeds = true;
        public RefreshToken? Saved;
        public string? LookedUpEmail;
        public string? OldHash;
        public DateTimeOffset RotationTime;
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct) { LookedUpEmail = email; return Task.FromResult(User); }
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(User);
        public Task<User?> FindByRefreshAsync(string hash, DateTimeOffset now, CancellationToken ct) => Task.FromResult(User);
        public Task<bool> TryCreateAsync(User user, CancellationToken ct) { if (CreateSucceeds) User = user; return Task.FromResult(CreateSucceeds); }
        public Task SaveLoginAsync(User user, RefreshToken token, CancellationToken ct) { Saved = token; return Task.CompletedTask; }
        public Task<bool> TryRotateAsync(string hash, RefreshToken token, DateTimeOffset now, CancellationToken ct)
        { OldHash = hash; RotationTime = now; return Task.FromResult(RotateSucceeds); }
    }
}
