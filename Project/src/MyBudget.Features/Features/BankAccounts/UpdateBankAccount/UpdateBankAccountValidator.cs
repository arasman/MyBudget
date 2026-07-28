using FluentValidation;

namespace MyBudget.Features.Features.BankAccounts.UpdateBankAccount;

public sealed class UpdateBankAccountValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.AccountId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Alias)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(100).WithErrorCode("ALIAS_TOO_LONG");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithErrorCode("DISPLAY_ORDER_INVALID");
    }
}
