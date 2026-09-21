namespace WebApis.Endpoints.WorkItemModule.Dependencies;

public sealed class AddDependencyRequest
{
    public Guid Id { get; set; }
    public Guid DependsOnWorkItemId { get; set; }
}
