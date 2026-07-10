using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.SetPeriodStatus;

public sealed class SetPeriodStatusValidator : AbstractValidator<SetPeriodStatusCommand>
{
    public SetPeriodStatusValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
