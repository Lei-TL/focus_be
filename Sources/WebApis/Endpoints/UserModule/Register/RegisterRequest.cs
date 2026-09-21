namespace WebApis.Endpoints.UserModule.Register;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int DefaultSessionMinutes { get; set; } = 50;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
}
