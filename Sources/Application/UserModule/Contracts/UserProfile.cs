using Domain.Entities.UserModule;
namespace Application.UserModule.Contracts;

public sealed record UserProfile(Guid Id, string Email, string DisplayName, long Seed,
    int DefaultSessionMinutes, string TimeZoneId, string Role)
{
    public static UserProfile From(User user) => new(user.Id, user.Email, user.DisplayName,
        user.Seed, user.DefaultSessionMinutes, user.TimeZoneId, user.Role.ToString());
}
