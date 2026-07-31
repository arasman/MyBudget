namespace MyBudget.Features.Features.Dashboard.GetCutTotalsBand;

/// <summary>
/// Pure static arithmetic for the DASH-2 lifetime average band. Stage 1 (grouping
/// CutRecord totals by Period, averaging WITHIN each period) happens in SQL —
/// see GetCutTotalsBandHandler. This calculator performs stage 2 only: AVG/MIN/MAX
/// of those per-period averages, computed ACROSS periods (design.md Decision 4 —
/// this MUST NEVER be a flat average across individual cuts).
/// No DB access — unit-testable in isolation (mirrors CutTotalsCalculator convention).
/// </summary>
public static class CutTotalsBandCalculator
{
    public static TotalsBandDto Compute(IReadOnlyList<PeriodAverageDto> periods)
    {
        if (periods.Count == 0)
        {
            return TotalsBandDto.Zero;
        }

        return new TotalsBandDto(
            Band(periods, a => a.TotalPositive),        Band(periods, a => a.TotalPositiveAlt),
            Band(periods, a => a.TotalNegative),         Band(periods, a => a.TotalNegativeAlt),
            Band(periods, a => a.TotalDeudaEnCurso),     Band(periods, a => a.TotalDeudaEnCursoAlt),
            Band(periods, a => a.TotalBudgeted),         Band(periods, a => a.TotalBudgetedAlt),
            Band(periods, a => a.TotalRegistered),       Band(periods, a => a.TotalRegisteredAlt),
            Band(periods, a => a.Remaining),             Band(periods, a => a.RemainingAlt),
            Band(periods, a => a.TotalAvailable),        Band(periods, a => a.TotalAvailableAlt),
            Band(periods, a => a.TotalNet),              Band(periods, a => a.TotalNetAlt));
    }

    private static BandValue Band(
        IReadOnlyList<PeriodAverageDto> periods, Func<ConceptTotalsDto, decimal> selector)
    {
        var values = periods.Select(p => selector(p.Avg)).ToList();
        return new BandValue(values.Average(), values.Min(), values.Max());
    }
}
