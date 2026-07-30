using FluentValidation;

namespace MyBudget.Features.Features.BankAccounts.RestoreBankAccount;

public sealed class RestoreBankAccountValidator : AbstractValidator<RestoreBankAccountCommand>
{
    public RestoreBankAccountValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.AccountId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
