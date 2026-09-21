using Domain.Enums;
namespace Application.WorkItemModule.Contracts;

public sealed record WorkItemFilter(WorkItemStatus? Status, TaskType? Type, int Page, int PageSize)
{
    // Validator HTTP đã chứng minh tên enum nên Parse an toàn.
    public static WorkItemFilter FromRaw(string? status, string? type, int page, int pageSize) => new(
        status is null ? null : Enum.Parse<WorkItemStatus>(status),
        type is null ? null : Enum.Parse<TaskType>(type), page, pageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
