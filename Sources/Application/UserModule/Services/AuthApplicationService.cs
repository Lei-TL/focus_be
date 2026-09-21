using Application.UserModule.Abstractions;
using Application.UserModule.Contracts;
using Domain.Entities.UserModule;
namespace Application.UserModule.Services;

public sealed class AuthApplicationService(
    IUserAuthStore store, IPasswordService passwords, ITokenIssuer tokens,
    ISeedGenerator seeds, TimeProvider clock)
{
    public async Task<UserProfile?> RegisterAsync(RegisterUser command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        if (await store.FindByEmailAsync(email, ct) is not null) return null;
        var user = new User {
            Email = email, DisplayName = command.DisplayName.Trim(), Seed = seeds.Create(),
            DefaultSessionMinutes = command.DefaultSessionMinutes, TimeZoneId = command.TimeZoneId
        };
        user.PasswordHash = passwords.Hash(user, command.Password);
        return await store.TryCreateAsync(user, ct) ? UserProfile.From(user) : null;
    }

    public async Task<TokenPair?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await store.FindByEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (user is null) return null;
        var verified = passwords.Verify(user, password);
        if (verified == PasswordCheck.Failed) return null;
        if (verified == PasswordCheck.RehashNeeded) user.PasswordHash = passwords.Hash(user, password);
        var issued = tokens.Issue(user, clock.GetUtcNow());
        await store.SaveLoginAsync(user, issued.Record, ct);
        return issued.Tokens;
    }

    public async Task<TokenPair?> RefreshAsync(string rawToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = tokens.HashRefreshToken(rawToken);
        var user = await store.FindByRefreshAsync(hash, now, ct);
        if (user is null) return null;
        var issued = tokens.Issue(user, now);
        return await store.TryRotateAsync(hash, issued.Record, now, ct) ? issued.Tokens : null;
    }

    public async Task<UserProfile?> GetProfileAsync(Guid id, CancellationToken ct)
    {
        var user = await store.FindByIdAsync(id, ct);
        return user is null ? null : UserProfile.From(user);
    }
}
