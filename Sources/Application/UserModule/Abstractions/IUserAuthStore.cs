using Domain.Entities.UserModule;
namespace Application.UserModule.Abstractions;

public interface IUserAuthStore
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<User?> FindByRefreshAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
    Task<bool> TryCreateAsync(User user, CancellationToken ct);
    Task SaveLoginAsync(User user, RefreshToken token, CancellationToken ct);
    // Consume old token and persist successor atomically; return false when already consumed/expired.
    Task<bool> TryRotateAsync(string oldHash, RefreshToken successor, DateTimeOffset now, CancellationToken ct);
}
