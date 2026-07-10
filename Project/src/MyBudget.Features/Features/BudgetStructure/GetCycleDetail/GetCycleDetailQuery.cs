using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.GetCycleDetail;

public sealed record GetCycleDetailQuery(Guid BudgetId, Guid CycleId)
    : IRequest<Result<CycleDetailResponse>>;

public sealed record CycleDetailResponse(
    Guid                         Id,
    string                       Name,
    DateOnly                     StartDate,
    DateOnly                     EndDate,
    bool                         IsActive,
    IReadOnlyList<PeriodSummary> Periods);

public sealed record PeriodSummary(
    Guid     Id,
    string   Name,
    int      PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    bool     IsClosed);
