using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineDateRange;

/// <summary>
/// REQ-BL-DATERANGE-1: Validates UpdateBudgetLineDateRangeCommand.
/// Date-range orphan guards are enforced in the handler (require DB and domain logic).
/// </summary>
public sealed class UpdateBudgetLineDateRangeValidator
    : AbstractValidator<UpdateBudgetLineDateRangeCommand>
{
    public UpdateBudgetLineDateRangeValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.LineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.EndDate)
            .Must((cmd, endDate) => endDate!.Value > cmd.StartDate)
                .WithErrorCode("FIELD_INVALID")
                .WithMessage("EndDate must be after StartDate.")
            .When(x => x.EndDate.HasValue);
    }
}
