using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Create;

public sealed class CreateWorkItemEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<CreateWorkItemRequest, IResult>
{
    public override void Configure() => Post("/work-items");

    public override async Task<IResult> ExecuteAsync(CreateWorkItemRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var detail = await service.CreateAsync(CreateWorkItem.FromRaw(request.Title,
            request.Description, request.Type!, request.Complexity!, request.Deadline,
            request.UserEstimateMinutes), ownerId, ct);
        return Results.Created($"/work-items/{detail.Id}", detail);
    }
}
