using Application.WorkItemModule.Contracts;
using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.WorkItemModule.List;

public sealed class ListWorkItemsValidator : Validator<ListWorkItemsRequest>
{
    public ListWorkItemsValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || WorkItemEnums.IsStatus(s))
            .WithMessage("Status must be one of: " + WorkItemEnums.Statuses);
        RuleFor(x => x.Type).Must(t => t is null || WorkItemEnums.IsTaskType(t))
            .WithMessage("Type must be one of: " + WorkItemEnums.TaskTypes);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
