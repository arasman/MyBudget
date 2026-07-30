using FluentValidation;

namespace MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;

public sealed class UpsertCutRecordValidator : AbstractValidator<UpsertCutRecordCommand>
{
    public UpsertCutRecordValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0).WithErrorCode("EXCHANGE_RATE_MUST_BE_POSITIVE");

        RuleForEach(x => x.Accounts).ChildRules(item =>
        {
            item.RuleFor(a => a.BankAccountId)
                .NotEmpty().WithErrorCode("FIELD_REQUIRED");

            item.RuleFor(a => a.Balance)
                .GreaterThanOrEqualTo(0).WithErrorCode("BALANCE_MUST_BE_NON_NEGATIVE");
        });
    }
}
