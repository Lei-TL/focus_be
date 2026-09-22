using Application.Authentication;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using FastEndpoints;
namespace WebApis.Endpoints.WorkItemModule.List;

public sealed class ListWorkItemsEndpoint(WorkItemApplicationService service, ICurrentUser current)
    : Endpoint<ListWorkItemsRequest, IResult>
{
    public override void Configure() => Get("/work-items");

    public override async Task<IResult> ExecuteAsync(ListWorkItemsRequest request, CancellationToken ct)
    {
        if (current.UserId is not Guid ownerId) return Results.Unauthorized();
        var result = await service.ListAsync(WorkItemFilter.FromRaw(request.Status, request.Type,
            request.Page, request.PageSize), ownerId, ct);
        return Results.Ok(result);
    }
}
