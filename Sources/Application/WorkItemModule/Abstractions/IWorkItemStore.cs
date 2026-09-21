using Domain.Entities.WorkItemModule;
using Domain.Enums;
namespace Application.WorkItemModule.Abstractions;

public interface IWorkItemStore
{
    Task AddAsync(WorkItem item, CancellationToken ct);
    Task<(IReadOnlyList<WorkItem> Items, int TotalCount)> ListAsync(Guid ownerId,
        WorkItemStatus? status, TaskType? type, int page, int pageSize, CancellationToken ct);
    // Đúng 2 query hằng số (item + danh sách link id), không N+1.
    Task<(WorkItem? Item, IReadOnlyList<Guid> DependsOnIds)> FindAsync(Guid ownerId, Guid id, CancellationToken ct);
}
