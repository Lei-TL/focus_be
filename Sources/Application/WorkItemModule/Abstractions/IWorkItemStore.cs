using Domain.Entities.WorkItemModule;
using Domain.Enums;
namespace Application.WorkItemModule.Abstractions;

public interface IWorkItemStore
{
    Task AddAsync(WorkItem item, CancellationToken ct);
    Task<(IReadOnlyList<WorkItem> Items, int TotalCount)> ListAsync(Guid ownerId,
        WorkItemStatus? status, TaskType? type, int page, int pageSize, CancellationToken ct);
}
