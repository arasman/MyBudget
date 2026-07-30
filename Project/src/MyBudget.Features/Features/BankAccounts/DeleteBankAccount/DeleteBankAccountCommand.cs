using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.DeleteBankAccount;

public sealed record DeleteBankAccountCommand(
    Guid BudgetId,
    Guid AccountId
) : IRequest<Result<bool>>;
