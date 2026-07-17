using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListPeriods;

public sealed record ListPeriodsQuery(
    Guid BudgetId,
    Guid CycleId,
    bool IncludeDeleted = false)
    : IRequest<Result<IReadOnlyList<PeriodListItem>>>;

public sealed record PeriodListItem(
    Guid             Id,
    string           Name,
    int              PeriodNumber,
    DateOnly         StartDate,
    DateOnly         EndDate,
    bool             IsClosed,
    DateTimeOffset?  DeletedAt = null);
