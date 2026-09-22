using Application.WorkItemModule.Contracts;
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
    // Tải entity tracked của owner, áp thay đổi rồi lưu; null khi không thấy. Không optimistic token ở M2.
    Task<WorkItem?> UpdateAsync(Guid ownerId, Guid id, Action<WorkItem> apply, CancellationToken ct);
    // Khóa row owner, re-check trong transaction, DFS chu trình rồi insert nguyên tử.
    Task<AddDependencyOutcome> TryAddDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct);
    // Cùng cơ chế khóa với add; xóa đúng cạnh có hướng, vắng cạnh vẫn thành công (idempotent).
    Task<RemoveDependencyOutcome> TryRemoveDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct);
    // Hard delete M2: xóa mọi link ở cả hai đầu rồi xóa item trong cùng transaction.
    Task<bool> TryDeleteAsync(Guid ownerId, Guid id, CancellationToken ct);
}
