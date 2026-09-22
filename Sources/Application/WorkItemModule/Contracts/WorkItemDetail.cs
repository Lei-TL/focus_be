using Domain.Entities.WorkItemModule;
namespace Application.WorkItemModule.Contracts;

public sealed record WorkItemDetail(Guid Id, string Title, string? Description, string Type,
    string Complexity, DateTimeOffset? Deadline, string Status, int? UserEstimateMinutes,
    DateTimeOffset? ClosedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static WorkItemDetail From(WorkItem item) => new(item.Id, item.Title, item.Description,
        item.Type.ToString(), item.Complexity.ToString(), item.Deadline, item.Status.ToString(),
        item.UserEstimateMinutes, item.ClosedAt, item.CreatedAt, item.UpdatedAt);
}
