using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.SetActiveCycle;

public sealed class SetActiveCycleValidator : AbstractValidator<SetActiveCycleCommand>
{
    public SetActiveCycleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
