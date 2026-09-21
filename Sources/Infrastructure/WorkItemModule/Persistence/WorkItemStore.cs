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

    public async Task<(WorkItem? Item, IReadOnlyList<Guid> DependsOnIds)> FindAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == ownerId, ct);
        if (item is null) return (null, []);
        // Phòng thủ sâu: scope links theo owner qua join, không chỉ dựa vào
        // giả định M2_06 bảo đảm link cùng owner.
        var dependsOn = await db.WorkItemLinks.AsNoTracking()
            .Where(x => x.WorkItemId == id && x.WorkItem.UserId == ownerId)
            .OrderBy(x => x.DependsOnWorkItemId)
            .Select(x => x.DependsOnWorkItemId).ToListAsync(ct);
        return (item, dependsOn);
    }
}
