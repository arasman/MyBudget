using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.RestoreBankAccount;

public sealed record RestoreBankAccountCommand(
    Guid BudgetId,
    Guid AccountId
) : IRequest<Result<Guid>>;
