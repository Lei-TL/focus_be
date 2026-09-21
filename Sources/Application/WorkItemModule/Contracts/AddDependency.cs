namespace Application.WorkItemModule.Contracts;

public enum AddDependencyOutcome { Added, ItemNotFound, SelfLoop, Duplicate, Cycle }

public sealed record DependencyDetail(Guid WorkItemId, Guid DependsOnWorkItemId);
