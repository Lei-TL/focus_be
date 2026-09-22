using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Dependencies;

public sealed class RemoveDependencyEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<RemoveDependencyRequest, IResult>
{
    public override void Configure() => Delete("/work-items/{id}/dependencies/{dependsOnWorkItemId}");

    public override async Task<IResult> ExecuteAsync(RemoveDependencyRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var outcome = await service.RemoveDependencyAsync(ownerId, request.Id, request.DependsOnWorkItemId, ct);
        return outcome == RemoveDependencyOutcome.Removed ? Results.NoContent() : Results.NotFound();
    }
}
