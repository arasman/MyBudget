using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;

public sealed class RestoreBudgetLineValidator : AbstractValidator<RestoreBudgetLineCommand>
{
    public RestoreBudgetLineValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.BudgetLineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
