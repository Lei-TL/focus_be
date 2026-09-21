using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.UserModule.Abstractions;
using Application.UserModule.Contracts;
using Domain.Entities.UserModule;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Infrastructure.UserModule.Security;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Pure generation: no DbContext writes; persistence belongs to IUserAuthStore.
    public IssuedTokens Issue(User user, DateTimeOffset now)
    {
        var settings = options.Value;
        var accessExpiry = now.AddMinutes(settings.AccessMinutes);
        var refreshExpiry = now.AddDays(settings.RefreshDays);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("sub", user.Id.ToString()), new Claim("role", user.Role.ToString()),
             new Claim("jti", Guid.NewGuid().ToString())], now.UtcDateTime, accessExpiry.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new IssuedTokens(
            new TokenPair(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, accessExpiry, refreshExpiry),
            new RefreshToken { UserId = user.Id, TokenHash = HashRefreshToken(refresh), ExpiresAt = refreshExpiry });
    }
}
