using Application.UserModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.UserModule.Refresh;

public sealed class RefreshEndpoint(AuthApplicationService service) : Endpoint<RefreshRequest, IResult>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }
    public override async Task<IResult> ExecuteAsync(RefreshRequest request, CancellationToken ct)
    {
        var tokens = await service.RefreshAsync(request.RefreshToken, ct);
        return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
    }
}
