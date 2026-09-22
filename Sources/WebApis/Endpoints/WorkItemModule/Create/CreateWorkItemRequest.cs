namespace WebApis.Endpoints.WorkItemModule.Create;

public sealed class CreateWorkItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Complexity { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public int? UserEstimateMinutes { get; set; }
}
