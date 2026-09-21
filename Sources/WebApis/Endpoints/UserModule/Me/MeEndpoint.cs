using Application.Authentication;
using Application.UserModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.UserModule.Me;

public sealed class MeEndpoint(AuthApplicationService service, ICurrentUser current) : EndpointWithoutRequest<IResult>
{
    public override void Configure() => Get("/auth/me");
    public override async Task<IResult> ExecuteAsync(CancellationToken ct)
    {
        if (current.UserId is not Guid id) return Results.Unauthorized();
        var profile = await service.GetProfileAsync(id, ct);
        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }
}
