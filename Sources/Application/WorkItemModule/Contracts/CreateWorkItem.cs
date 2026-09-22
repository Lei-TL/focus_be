using Domain.Enums;
namespace Application.WorkItemModule.Contracts;

public sealed record CreateWorkItem(string Title, string? Description, TaskType Type,
    Complexity Complexity, DateTimeOffset? Deadline, int? UserEstimateMinutes)
{
    // Validator HTTP đã chứng minh type/complexity đúng tên enum nên Parse an toàn.
    // Đặt ở Application để endpoint không phải tham chiếu Domain.
    public static CreateWorkItem FromRaw(string title, string? description, string type,
        string complexity, DateTimeOffset? deadline, int? userEstimateMinutes) => new(title,
        description, Enum.Parse<TaskType>(type), Enum.Parse<Complexity>(complexity),
        deadline, userEstimateMinutes);
}
