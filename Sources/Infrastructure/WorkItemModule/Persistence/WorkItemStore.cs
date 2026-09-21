using Application.WorkItemModule.Abstractions;
using Domain.Entities.WorkItemModule;
using Infrastructure.Persistence;
namespace Infrastructure.WorkItemModule.Persistence;

public sealed class WorkItemStore(FocusDbContext db) : IWorkItemStore
{
    public async Task AddAsync(WorkItem item, CancellationToken ct)
    {
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
    }
}
