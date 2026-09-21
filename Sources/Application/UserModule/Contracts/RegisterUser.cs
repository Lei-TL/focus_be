namespace Application.UserModule.Contracts;

public sealed record RegisterUser(string Email, string Password, string DisplayName,
    int DefaultSessionMinutes, string TimeZoneId);
