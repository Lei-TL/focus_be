using Application.WorkItemModule.Abstractions;
using Application.WorkItemModule.Contracts;
using Domain.Entities.WorkItemModule;
using Domain.Enums;
namespace Application.WorkItemModule.Services;

public sealed class WorkItemApplicationService(IWorkItemStore store, TimeProvider clock)
{
    public async Task<WorkItemDetail> CreateAsync(CreateWorkItem command, Guid ownerId, CancellationToken ct)
    {
        var item = new WorkItem {
            UserId = ownerId,
            Title = command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            Type = command.Type,
            Complexity = command.Complexity,
            Deadline = command.Deadline?.ToUniversalTime(),
            Status = WorkItemStatus.Open,
            UserEstimateMinutes = command.UserEstimateMinutes,
            ClosedAt = null
        };
        await store.AddAsync(item, ct);
        return WorkItemDetail.From(item);
    }

    public async Task<PagedResult<WorkItemDetail>> ListAsync(WorkItemFilter filter, Guid ownerId, CancellationToken ct)
    {
        var (items, total) = await store.ListAsync(ownerId, filter.Status, filter.Type,
            filter.Page, filter.PageSize, ct);
        return new PagedResult<WorkItemDetail>(
            items.Select(WorkItemDetail.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<WorkItemDetailWithLinks?> GetAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var (item, dependsOn) = await store.FindAsync(ownerId, id, ct);
        return item is null ? null : WorkItemDetailWithLinks.From(item, dependsOn);
    }

    public async Task<WorkItemDetail?> UpdateAsync(UpdateWorkItem command, Guid ownerId, Guid id, CancellationToken ct)
    {
        var updated = await store.UpdateAsync(ownerId, id, item => {
            item.Title = command.Title.Trim();
            item.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
            item.Type = command.Type;
            item.Complexity = command.Complexity;
            item.Deadline = command.Deadline?.ToUniversalTime();
            item.UserEstimateMinutes = command.UserEstimateMinutes;
        }, ct);
        return updated is null ? null : WorkItemDetail.From(updated);
    }

    public async Task<WorkItemDetail?> ChangeStatusAsync(ChangeWorkItemStatus command, Guid ownerId, Guid id, CancellationToken ct)
    {
        var updated = await store.UpdateAsync(ownerId, id, item => {
            if (item.Status == command.Status) return;
            item.Status = command.Status;
            item.ClosedAt = command.Status switch {
                WorkItemStatus.Done => clock.GetUtcNow(),
                WorkItemStatus.Open => null,
                _ => item.ClosedAt
            };
        }, ct);
        return updated is null ? null : WorkItemDetail.From(updated);
    }

    public Task<AddDependencyOutcome> AddDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct) =>
        // Self-link suy ra trực tiếp từ request, không cần transaction.
        workItemId == dependsOnId
            ? Task.FromResult(AddDependencyOutcome.SelfLoop)
            : store.TryAddDependencyAsync(ownerId, workItemId, dependsOnId, ct);
}
