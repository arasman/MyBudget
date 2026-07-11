using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLines;

public sealed record ListBudgetLinesQuery(Guid BudgetId, Guid PeriodId)
    : IRequest<Result<IReadOnlyList<BudgetLineResponse>>>;

public sealed record BudgetLineResponse(
    Guid      Id,
    string    Name,
    string    LineType,
    bool      IsRecurring,
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    decimal?  BudgetedAmount,
    string?   CurrencyCode,
    string?   CurrencySymbol,
    DateTimeOffset? RevisedAt,
    string?   Note);
