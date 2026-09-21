using Application.UserModule.Abstractions;
using Domain.Entities.UserModule;
using Microsoft.AspNetCore.Identity;
namespace Infrastructure.UserModule.Security;

public sealed class IdentityPasswordService(IPasswordHasher<User> hasher) : IPasswordService
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);
    public PasswordCheck Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) switch {
            PasswordVerificationResult.Success => PasswordCheck.Valid,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.RehashNeeded,
            _ => PasswordCheck.Failed
        };
}
