using Domain.Entities;
using Domain.Enums;
namespace Domain.Entities.UserModule;
public sealed class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Seed { get; set; }
    public int DefaultSessionMinutes { get; set; } = 50;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public UserRole Role { get; set; } = UserRole.User;
}
