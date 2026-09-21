using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.UserModule.Login;

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
