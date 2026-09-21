using Domain.Entities.UserModule;
namespace Application.UserModule.Contracts;

public sealed record IssuedTokens(TokenPair Tokens, RefreshToken Record);
