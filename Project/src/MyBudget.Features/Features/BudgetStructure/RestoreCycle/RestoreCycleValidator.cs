using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCycle;

public sealed class RestoreCycleValidator : AbstractValidator<RestoreCycleCommand>
{
    public RestoreCycleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
