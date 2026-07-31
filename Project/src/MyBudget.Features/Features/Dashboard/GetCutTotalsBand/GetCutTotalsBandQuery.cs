using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetCutTotalsBand;

public sealed record GetCutTotalsBandQuery(
    Guid BudgetId
) : IRequest<Result<CutTotalsBandResponse>>;

public sealed record CutTotalsBandResponse(
    string                            ConversionBasis,
    int                               PeriodCount,
    IReadOnlyList<PeriodAverageDto>  Periods,
    TotalsBandDto                     Band);

/// <summary>
/// Stage-1 result: one CutRecord total average PER Period (Decision 4 — never a flat
/// average across individual cuts). Periods with zero contained cuts never appear here.
/// </summary>
public sealed record PeriodAverageDto(
    Guid             PeriodId,
    DateOnly         PeriodStart,
    DateOnly         PeriodEnd,
    ConceptTotalsDto Avg);

public sealed record ConceptTotalsDto(
    decimal TotalPositive,      decimal TotalPositiveAlt,
    decimal TotalNegative,      decimal TotalNegativeAlt,
    decimal TotalDeudaEnCurso,  decimal TotalDeudaEnCursoAlt,
    decimal TotalBudgeted,      decimal TotalBudgetedAlt,
    decimal TotalRegistered,    decimal TotalRegisteredAlt,
    decimal Remaining,          decimal RemainingAlt,
    decimal TotalAvailable,     decimal TotalAvailableAlt,
    decimal TotalNet,           decimal TotalNetAlt);

public sealed record BandValue(decimal Avg, decimal Min, decimal Max)
{
    public static readonly BandValue Zero = new(0m, 0m, 0m);
}

/// <summary>
/// Stage-2 result: AVG/MIN/MAX of the per-period averages (<see cref="PeriodAverageDto"/>),
/// computed ACROSS periods — the lifetime deviation band (DASH-2).
/// </summary>
public sealed record TotalsBandDto(
    BandValue TotalPositive,      BandValue TotalPositiveAlt,
    BandValue TotalNegative,      BandValue TotalNegativeAlt,
    BandValue TotalDeudaEnCurso,  BandValue TotalDeudaEnCursoAlt,
    BandValue TotalBudgeted,      BandValue TotalBudgetedAlt,
    BandValue TotalRegistered,    BandValue TotalRegisteredAlt,
    BandValue Remaining,          BandValue RemainingAlt,
    BandValue TotalAvailable,     BandValue TotalAvailableAlt,
    BandValue TotalNet,           BandValue TotalNetAlt)
{
    public static readonly TotalsBandDto Zero = new(
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero,
        BandValue.Zero, BandValue.Zero);
}
