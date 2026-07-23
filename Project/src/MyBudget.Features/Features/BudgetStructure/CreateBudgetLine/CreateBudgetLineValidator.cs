using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public sealed class CreateBudgetLineValidator : AbstractValidator<CreateBudgetLineCommand>
{
    public CreateBudgetLineValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.LineType)
            .Must(lt => Enum.IsDefined(typeof(LineType), lt))
            .WithErrorCode("FIELD_INVALID");

        // REQ-BL-02: StartDate required
        RuleFor(x => x.StartDate)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        // REQ-BL-02: EndDate, when provided, must be strictly after StartDate
        RuleFor(x => x.EndDate)
            .Must((cmd, endDate) => endDate!.Value > cmd.StartDate)
            .WithErrorCode("FIELD_INVALID")
            .When(x => x.EndDate.HasValue);

        // REQ-BL-02: InitialAmount must be > 0
        RuleFor(x => x.InitialAmount)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithErrorCode("FIELD_INVALID")
            .When(x => x.Description is not null);
    }
}
