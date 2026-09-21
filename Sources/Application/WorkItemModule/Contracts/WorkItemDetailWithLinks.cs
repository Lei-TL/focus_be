using Domain.Entities.WorkItemModule;
namespace Application.WorkItemModule.Contracts;

public sealed record WorkItemDetailWithLinks(Guid Id, string Title, string? Description,
    string Type, string Complexity, DateTimeOffset? Deadline, string Status,
    int? UserEstimateMinutes, DateTimeOffset? ClosedAt, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<Guid> DependsOnWorkItemIds)
{
    public static WorkItemDetailWithLinks From(WorkItem item, IReadOnlyList<Guid> dependsOn) =>
        new(item.Id, item.Title, item.Description, item.Type.ToString(),
            item.Complexity.ToString(), item.Deadline, item.Status.ToString(),
            item.UserEstimateMinutes, item.ClosedAt, item.CreatedAt, item.UpdatedAt, dependsOn);
}
