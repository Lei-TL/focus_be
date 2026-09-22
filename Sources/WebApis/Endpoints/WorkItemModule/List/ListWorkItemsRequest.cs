namespace WebApis.Endpoints.WorkItemModule.List;

public sealed class ListWorkItemsRequest
{
    public string? Status { get; set; }
    public string? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
