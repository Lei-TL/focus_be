using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.UserModule.Refresh;

public sealed class RefreshValidator : Validator<RefreshRequest>
{
    public RefreshValidator() { RuleFor(x => x.RefreshToken).NotEmpty().Length(64); }
}
