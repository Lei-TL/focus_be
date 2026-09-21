using Domain.Enums;
namespace Application.WorkItemModule.Contracts;

public sealed record ChangeWorkItemStatus(WorkItemStatus Status)
{
    public static ChangeWorkItemStatus FromRaw(string status) =>
        new(Enum.Parse<WorkItemStatus>(status));
}
