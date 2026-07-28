using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.CreateBankAccount;

public sealed record CreateBankAccountCommand(
    Guid   BudgetId,
    Guid   CurrencyId,
    string Alias,
    bool   IsPositive,
    int    DisplayOrder
) : IRequest<Result<Guid>>;
