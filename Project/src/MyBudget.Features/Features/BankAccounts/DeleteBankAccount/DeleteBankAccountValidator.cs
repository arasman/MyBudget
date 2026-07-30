using FluentValidation;

namespace MyBudget.Features.Features.BankAccounts.DeleteBankAccount;

public sealed class DeleteBankAccountValidator : AbstractValidator<DeleteBankAccountCommand>
{
    public DeleteBankAccountValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.AccountId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
