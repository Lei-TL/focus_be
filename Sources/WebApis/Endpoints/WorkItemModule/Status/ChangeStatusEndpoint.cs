using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Status;

public sealed class ChangeStatusEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<ChangeStatusRequest, IResult>
{
    public override void Configure() => Patch("/work-items/{id}/status");

    public override async Task<IResult> ExecuteAsync(ChangeStatusRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var detail = await service.ChangeStatusAsync(
            ChangeWorkItemStatus.FromRaw(request.Status!), ownerId, request.Id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }
}
