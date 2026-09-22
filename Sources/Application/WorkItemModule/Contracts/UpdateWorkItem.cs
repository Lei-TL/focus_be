using Domain.Enums;
namespace Application.WorkItemModule.Contracts;

public sealed record UpdateWorkItem(string Title, string? Description, TaskType Type,
    Complexity Complexity, DateTimeOffset? Deadline, int? UserEstimateMinutes)
{
    public static UpdateWorkItem FromRaw(string title, string? description, string type,
        string complexity, DateTimeOffset? deadline, int? userEstimateMinutes) => new(title,
        description, Enum.Parse<TaskType>(type), Enum.Parse<Complexity>(complexity),
        deadline, userEstimateMinutes);
}
