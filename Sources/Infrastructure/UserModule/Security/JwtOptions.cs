namespace Infrastructure.UserModule.Security;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Focus";
    public string Audience { get; set; } = "Focus.App";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 30;
}
