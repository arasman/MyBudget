using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.RestorePeriod;

public sealed class RestorePeriodValidator : AbstractValidator<RestorePeriodCommand>
{
    public RestorePeriodValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
