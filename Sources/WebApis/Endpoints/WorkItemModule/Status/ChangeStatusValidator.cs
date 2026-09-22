using Application.WorkItemModule.Contracts;
using FastEndpoints;
using FluentValidation;
namespace WebApis.Endpoints.WorkItemModule.Status;

public sealed class ChangeStatusValidator : Validator<ChangeStatusRequest>
{
    public ChangeStatusValidator()
    {
        RuleFor(x => x.Status).NotEmpty().Must(WorkItemEnums.IsStatus)
            .WithMessage("Status must be one of: " + WorkItemEnums.Statuses);
    }
}
