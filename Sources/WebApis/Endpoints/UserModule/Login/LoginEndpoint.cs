using Application.UserModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.UserModule.Login;

public sealed class LoginEndpoint(AuthApplicationService service) : Endpoint<LoginRequest, IResult>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }
    public override async Task<IResult> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        var tokens = await service.LoginAsync(request.Email, request.Password, ct);
        return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
    }
}
