using Application.Authentication;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Delete;

public sealed class DeleteWorkItemEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<DeleteWorkItemRequest, IResult>
{
    public override void Configure() => Delete("/work-items/{id}");

    public override async Task<IResult> ExecuteAsync(DeleteWorkItemRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        return await service.DeleteAsync(ownerId, request.Id, ct) ? Results.NoContent() : Results.NotFound();
    }
}
