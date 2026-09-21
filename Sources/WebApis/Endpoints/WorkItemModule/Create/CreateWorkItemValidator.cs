using Application.WorkItemModule.Contracts;
using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.WorkItemModule.Create;

public sealed class CreateWorkItemValidator : Validator<CreateWorkItemRequest>
{
    public CreateWorkItemValidator(TimeProvider clock)
    {
        RuleFor(x => x.Title).NotEmpty().Must(title => title.Trim().Length <= 200)
            .WithMessage("Title must be 1-200 characters after trimming.");
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Type).NotEmpty().Must(WorkItemEnums.IsTaskType)
            .WithMessage("Type must be one of: " + WorkItemEnums.TaskTypes);
        RuleFor(x => x.Complexity).NotEmpty().Must(WorkItemEnums.IsComplexity)
            .WithMessage("Complexity must be one of: " + WorkItemEnums.Complexities);
        RuleFor(x => x.UserEstimateMinutes).GreaterThan(0)
            .When(x => x.UserEstimateMinutes.HasValue);
        RuleFor(x => x.Deadline).Must(deadline => deadline > clock.GetUtcNow())
            .WithMessage("Deadline must not be in the past.")
            .When(x => x.Deadline.HasValue);
    }
}
