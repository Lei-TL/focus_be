using Application.WorkItemModule.Abstractions;
using Application.WorkItemModule.Contracts;
using Application.WorkItemModule.Services;
using Domain.Entities.WorkItemModule;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<WorkItem?> UpdateAsync(Guid ownerId, Guid id, Action<WorkItem> apply, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(x => x.Id == id && x.UserId == ownerId, ct);
        if (item is null) return null;
        apply(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<AddDependencyOutcome> TryAddDependencyAsync(Guid ownerId, Guid workItemId, Guid dependsOnId, CancellationToken ct)
    {
        // Serialize mọi thay đổi graph của cùng owner bằng khóa row users.
        // Owner khác không chặn nhau; thứ tự khóa cố định nên M2_07/08 dùng
        // chung cơ chế này không gây deadlock.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owner = await db.Users
            .FromSql($"SELECT * FROM users WHERE id = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (owner is null) return AddDependencyOutcome.ItemNotFound;

        var ownedIds = await db.WorkItems.AsNoTracking()
            .Where(x => x.UserId == ownerId && (x.Id == workItemId || x.Id == dependsOnId))
            .Select(x => x.Id).ToListAsync(ct);
        if (!ownedIds.Contains(workItemId) || !ownedIds.Contains(dependsOnId))
            return AddDependencyOutcome.ItemNotFound;

        var edges = (await db.WorkItemLinks.AsNoTracking()
            .Where(x => x.WorkItem.UserId == ownerId)
            .Select(x => new { x.WorkItemId, x.DependsOnWorkItemId }).ToListAsync(ct))
            .Select(x => (x.WorkItemId, x.DependsOnWorkItemId)).ToList();
        // Thêm A→B tạo vòng khi từ B đã tới được A theo chiều phụ thuộc.
        if (WorkItemGraph.HasPath(edges, dependsOnId, workItemId))
            return AddDependencyOutcome.Cycle;

        db.WorkItemLinks.Add(new WorkItemLink { WorkItemId = workItemId, DependsOnWorkItemId = dependsOnId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return AddDependencyOutcome.Duplicate;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return AddDependencyOutcome.ItemNotFound;
        }
        await tx.CommitAsync(ct);
        return AddDependencyOutcome.Added;
    }
}
