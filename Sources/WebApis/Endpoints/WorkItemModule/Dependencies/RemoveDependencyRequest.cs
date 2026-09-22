namespace WebApis.Endpoints.WorkItemModule.Dependencies;

public sealed class RemoveDependencyRequest
{
    public Guid Id { get; set; }
    public Guid DependsOnWorkItemId { get; set; }
}
