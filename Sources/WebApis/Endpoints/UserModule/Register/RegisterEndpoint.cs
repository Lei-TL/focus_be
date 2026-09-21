using Application.UserModule.Contracts;
using Application.UserModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.UserModule.Register;

public sealed class RegisterEndpoint(AuthApplicationService service) : Endpoint<RegisterRequest, IResult>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task<IResult> ExecuteAsync(RegisterRequest request, CancellationToken ct)
    {
        var profile = await service.RegisterAsync(new RegisterUser(request.Email, request.Password,
            request.DisplayName, request.DefaultSessionMinutes, request.TimeZoneId), ct);
        return profile is null ? Results.Conflict(new { error = "Email already registered." })
            : Results.Created("/auth/me", profile);
    }
}
