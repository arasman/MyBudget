using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetBudgetLineSeries;

/// <summary>
/// One or more BudgetLineIds compared across one or more PeriodIds (DASH-4/5/6). PeriodIds
/// may span a single Cycle (per-period behavior / period-vs-period) or two Cycles
/// (cycle-vs-cycle) — the query is the same either way; only which PeriodIds the client
/// sends differs (design.md non-obvious SQL constraints).
/// </summary>
public sealed record GetBudgetLineSeriesQuery(
    Guid   BudgetId,
    Guid[] LineIds,
    Guid[] PeriodIds
) : IRequest<Result<BudgetLineSeriesResponse>>;

public sealed record BudgetLineSeriesResponse(
    string                                 ConversionBasis,
    IReadOnlyList<PeriodSeriesDto>        Periods,
    IReadOnlyList<BudgetLineSeriesRowDto> Rows);

/// <summary>
/// One selected Period's metadata. Carries <see cref="DefaultCurrencyId"/> (DASH-12) —
/// sourced from Cycle.DefaultCurrencyId, NOT Budget — so the client can detect and warn on
/// cross-cycle currency mismatches instead of silently blending two currencies on one axis.
/// </summary>
public sealed record PeriodSeriesDto(
    Guid     PeriodId,
    Guid     CycleId,
    DateOnly PeriodStart,
    Guid     DefaultCurrencyId);

/// <summary>
/// One BudgetLine's budgeted/registered totals for one selected Period (DASH-4/5/6).
/// Cross-cycle identity is the BudgetLineId alone — BudgetLine is BudgetId-scoped, not
/// CycleId-scoped, so the same row persists across cycles via its Revisions collection
/// (design.md Decision 3) — no fuzzy matching, no "unmatched line" state.
/// </summary>
public sealed record BudgetLineSeriesRowDto(
    Guid    BudgetLineId,
    string  BudgetLineName,
    Guid    PeriodId,
    decimal BudgetedAmount,
    decimal NetTotal);
