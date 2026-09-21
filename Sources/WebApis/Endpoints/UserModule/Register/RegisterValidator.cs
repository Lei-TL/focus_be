using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.UserModule.Register;

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultSessionMinutes).InclusiveBetween(15, 90);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100).Must(IsIanaZone);
    }
    private static bool IsIanaZone(string? zone)
    {
        if (string.IsNullOrWhiteSpace(zone)) return false;
        try { return TimeZoneInfo.FindSystemTimeZoneById(zone).HasIanaId; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
