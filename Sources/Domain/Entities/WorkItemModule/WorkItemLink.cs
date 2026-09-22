namespace Domain.Entities.WorkItemModule;
public sealed class WorkItemLink : BaseEntity
{
    public Guid WorkItemId { get; set; }
    public Guid DependsOnWorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public WorkItem DependsOn { get; set; } = null!;
}
