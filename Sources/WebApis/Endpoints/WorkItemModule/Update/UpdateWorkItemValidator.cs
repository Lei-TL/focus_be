using Application.WorkItemModule.Contracts;
using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.WorkItemModule.Update;

// Cùng luật với create, trừ deadline quá khứ được phép khi sửa.
public sealed class UpdateWorkItemValidator : Validator<UpdateWorkItemRequest>
{
    public UpdateWorkItemValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Must(title => title is null || title.Trim().Length <= 200)
            .WithMessage("Title must be 1-200 characters after trimming.");
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Type).NotEmpty().Must(WorkItemEnums.IsTaskType)
            .WithMessage("Type must be one of: " + WorkItemEnums.TaskTypes);
        RuleFor(x => x.Complexity).NotEmpty().Must(WorkItemEnums.IsComplexity)
            .WithMessage("Complexity must be one of: " + WorkItemEnums.Complexities);
        RuleFor(x => x.UserEstimateMinutes).GreaterThan(0)
            .When(x => x.UserEstimateMinutes.HasValue);
    }
}
