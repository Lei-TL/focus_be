using Application.UserModule.Contracts;
using Domain.Entities.UserModule;
namespace Application.UserModule.Abstractions;

public interface ITokenIssuer
{
    string HashRefreshToken(string rawToken);
    IssuedTokens Issue(User user, DateTimeOffset now);
}
