namespace WebApis.Endpoints.WorkItemModule.Status;

public sealed class ChangeStatusRequest
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
}
