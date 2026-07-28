using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.UpdateBankAccount;

public sealed record UpdateBankAccountCommand(
    Guid   BudgetId,
    Guid   AccountId,
    string Alias,
    bool   IsPositive,
    int    DisplayOrder
) : IRequest<Result<bool>>;
