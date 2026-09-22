using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.Dependencies;

public sealed class AddDependencyEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<AddDependencyRequest, IResult>
{
    public override void Configure() => Post("/work-items/{id}/dependencies");

    public override async Task<IResult> ExecuteAsync(AddDependencyRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        // Guid.Empty không trỏ tới item nào nên rơi vào NotFound, cùng luật detail.
        var outcome = await service.AddDependencyAsync(ownerId, request.Id, request.DependsOnWorkItemId, ct);
        return outcome switch {
            AddDependencyOutcome.Added => Results.Created(
                $"/work-items/{request.Id}/dependencies/{request.DependsOnWorkItemId}",
                new DependencyDetail(request.Id, request.DependsOnWorkItemId)),
            AddDependencyOutcome.ItemNotFound => Results.NotFound(),
            AddDependencyOutcome.Duplicate => Results.Conflict(new { error = "Dependency already exists." }),
            AddDependencyOutcome.SelfLoop => Results.BadRequest(new { error = "A work item cannot depend on itself." }),
            _ => Results.BadRequest(new { error = "Dependency would create a cycle." })
        };
    }
}
