using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Update;

public sealed class UpdateWorkItemEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<UpdateWorkItemRequest, IResult>
{
    public override void Configure() => Put("/work-items/{id}");

    public override async Task<IResult> ExecuteAsync(UpdateWorkItemRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var detail = await service.UpdateAsync(UpdateWorkItem.FromRaw(request.Title,
            request.Description, request.Type!, request.Complexity!, request.Deadline,
            request.UserEstimateMinutes), ownerId, request.Id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }
}
