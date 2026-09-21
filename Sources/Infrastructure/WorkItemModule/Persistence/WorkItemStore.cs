using Application.WorkItemModule.Abstractions;
using Domain.Entities.WorkItemModule;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.WorkItemModule.Persistence;

public sealed class WorkItemStore(FocusDbContext db) : IWorkItemStore
{
    public async Task AddAsync(WorkItem item, CancellationToken ct)
    {
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<WorkItem> Items, int TotalCount)> ListAsync(Guid ownerId,
        WorkItemStatus? status, TaskType? type, int page, int pageSize, CancellationToken ct)
    {
        var query = db.WorkItems.AsNoTracking().Where(x => x.UserId == ownerId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
