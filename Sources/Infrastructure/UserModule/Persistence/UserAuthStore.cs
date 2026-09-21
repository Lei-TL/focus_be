using Application.UserModule.Abstractions;
using Domain.Entities.UserModule;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Infrastructure.UserModule.Persistence;

public sealed class UserAuthStore(FocusDbContext db) : IUserAuthStore
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<User?> FindByRefreshAsync(string hash, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now)
            .Select(x => x.User).SingleOrDefaultAsync(ct);

    public async Task<bool> TryCreateAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_email" })
        {
            db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public async Task SaveLoginAsync(User user, RefreshToken token, CancellationToken ct)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryRotateAsync(string oldHash, RefreshToken successor, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var consumed = await db.RefreshTokens
            .Where(x => x.TokenHash == oldHash && x.UserId == successor.UserId && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.UpdatedAt, now), ct);
        if (consumed != 1) return false;
        db.RefreshTokens.Add(successor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
