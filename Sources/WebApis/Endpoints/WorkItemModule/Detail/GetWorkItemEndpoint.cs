using Application.Authentication;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Detail;

public sealed class GetWorkItemEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<GetWorkItemRequest, IResult>
{
    public override void Configure() => Get("/work-items/{id}");

    public override async Task<IResult> ExecuteAsync(GetWorkItemRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var detail = await service.GetAsync(ownerId, request.Id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }
}
