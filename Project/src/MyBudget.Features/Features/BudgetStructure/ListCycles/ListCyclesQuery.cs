using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCycles;

public sealed record ListCyclesQuery(Guid BudgetId) : IRequest<Result<IReadOnlyList<CycleListItem>>>;

public sealed record CycleListItem(
    Guid     Id,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool     IsActive,
    int      PeriodCount);
