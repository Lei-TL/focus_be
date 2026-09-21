using Domain.Entities.WorkItemModule;
namespace Application.WorkItemModule.Abstractions;

public interface IWorkItemStore
{
    Task AddAsync(WorkItem item, CancellationToken ct);
}
