using Domain.Entities.UserModule;
using Domain.Enums;
namespace Domain.Entities.WorkItemModule;
public sealed class WorkItem : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public Complexity Complexity { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Open;
    public int? UserEstimateMinutes { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public User User { get; set; } = null!;
    public ICollection<WorkItemLink> Dependencies { get; set; } = new List<WorkItemLink>();
    public ICollection<WorkItemLink> DependedOnBy { get; set; } = new List<WorkItemLink>();
}
