using Domain.Enums;
namespace Application.WorkItemModule.Contracts;

// Endpoint/validator chỉ được phụ thuộc Application nên mọi tra cứu enum Domain
// cho HTTP validation/response đều đi qua helper này, không using Domain trực tiếp.
public static class WorkItemEnums
{
    public static string TaskTypes => string.Join(", ", Enum.GetNames<TaskType>());
    public static string Complexities => string.Join(", ", Enum.GetNames<Complexity>());
    public static string Statuses => string.Join(", ", Enum.GetNames<WorkItemStatus>());

    public static bool IsTaskType(string? value) =>
        value is not null && Enum.GetNames<TaskType>().Contains(value, StringComparer.Ordinal);

    public static bool IsComplexity(string? value) =>
        value is not null && Enum.GetNames<Complexity>().Contains(value, StringComparer.Ordinal);

    public static bool IsStatus(string? value) =>
        value is not null && Enum.GetNames<WorkItemStatus>().Contains(value, StringComparer.Ordinal);
}
