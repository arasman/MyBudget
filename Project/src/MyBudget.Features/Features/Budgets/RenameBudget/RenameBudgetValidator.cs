using FluentValidation;

namespace MyBudget.Features.Features.Budgets.RenameBudget;

public sealed class RenameBudgetValidator : AbstractValidator<RenameBudgetCommand>
{
    public RenameBudgetValidator()
    {
        RuleFor(x => x.NewName)
            .NotEmpty().WithErrorCode("BUDGET_NAME_REQUIRED")
            .MaximumLength(200).WithErrorCode("BUDGET_NAME_TOO_LONG");

        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
