using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCycle;

public sealed class UpdateCycleValidator : AbstractValidator<UpdateCycleCommand>
{
    public UpdateCycleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.StartDate)
            .Must((cmd, start) => start < cmd.EndDate)
            .WithErrorCode("CYCLE_INVALID_DATE_RANGE")
            .WithMessage("StartDate must be before EndDate.");
    }
}
