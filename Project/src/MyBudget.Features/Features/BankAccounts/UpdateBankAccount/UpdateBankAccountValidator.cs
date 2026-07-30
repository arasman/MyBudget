using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.Features.BankAccounts.UpdateBankAccount;

public sealed class UpdateBankAccountValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountValidator(AppDbContext db)
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

        RuleFor(x => x.Alias)
            .MustAsync(async (cmd, alias, ct) =>
            {
                return !await db.BankAccounts
                    .IgnoreQueryFilters()
                    .AnyAsync(a => a.BudgetId == cmd.BudgetId
                                && a.Alias == alias.Trim()
                                && a.Id != cmd.AccountId, ct);
            })
            .WithErrorCode("ALIAS_DUPLICATE")
            .When(x => !string.IsNullOrWhiteSpace(x.Alias));
    }
}
