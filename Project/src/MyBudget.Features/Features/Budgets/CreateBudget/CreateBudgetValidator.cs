using FluentValidation;

namespace MyBudget.Features.Features.Budgets.CreateBudget;

public sealed class CreateBudgetValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("BUDGET_NAME_REQUIRED")
            .MaximumLength(200).WithErrorCode("BUDGET_NAME_TOO_LONG");

        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
