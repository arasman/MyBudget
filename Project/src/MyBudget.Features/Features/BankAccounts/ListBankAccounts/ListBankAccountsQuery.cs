using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.ListBankAccounts;

public sealed record ListBankAccountsQuery(Guid BudgetId, bool IncludeDeleted = false)
    : IRequest<Result<IReadOnlyList<BankAccountDto>>>;

public sealed record BankAccountDto(
    Guid              Id,
    Guid              CurrencyId,
    string            Alias,
    bool              IsPositive,
    int               DisplayOrder,
    DateTimeOffset?   DeletedAt);
