namespace Application.UserModule.Contracts;

public sealed record TokenPair(string AccessToken, string RefreshToken,
    DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt);
