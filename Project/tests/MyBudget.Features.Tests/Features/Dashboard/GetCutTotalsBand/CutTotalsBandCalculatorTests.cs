using MyBudget.Features.Features.Dashboard.GetCutTotalsBand;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Dashboard.GetCutTotalsBand;

/// <summary>
/// Unit tests for CutTotalsBandCalculator — the DASH-2 stage-2 aggregation (AVG/MIN/MAX
/// across per-period averages). Stage-1 (grouping cuts by Period via SQL, date-containment
/// join for DASH-11) is covered by integration tests in MyBudget.Integration.Tests; this
/// class assumes the per-period averages already exist and only exercises the pure math.
/// </summary>
public sealed class CutTotalsBandCalculatorTests
{
    // ── DASH-2: two-stage averaging (spec scenario) ────────────────────────────
    // GIVEN Period A has cuts totaling [100, 200] (period avg 150) and Period B has one
    // cut totaling 300 (period avg 300) THEN AVG=225, MIN=150, MAX=300 — derived from the
    // two per-period averages, NOT the flat set [100, 200, 300] (flat avg would be 200).

    [Fact]
    public void Compute_TwoPeriods_ReturnsAvgMinMaxOfPeriodAveragesNotFlatCutAverage()
    {
        var periods = new[]
        {
            PeriodWith(periodAvg: 150m),
            PeriodWith(periodAvg: 300m),
        };

        var band = CutTotalsBandCalculator.Compute(periods);

        band.TotalPositive.Avg.ShouldBe(225m);
        band.TotalPositive.Min.ShouldBe(150m);
        band.TotalPositive.Max.ShouldBe(300m);
    }

    [Fact]
    public void Compute_EachConceptAggregatedIndependently()
    {
        var periods = new[]
        {
            new PeriodAverageDto(
                Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                new ConceptTotalsDto(
                    100m, 10m, 50m, 5m, 60m, 6m, 40m, 4m,
                    30m, 3m, 10m, 1m, 100m, 10m, 0m, 0m)),
            new PeriodAverageDto(
                Guid.NewGuid(), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28),
                new ConceptTotalsDto(
                    300m, 30m, 150m, 15m, 60m, 6m, 40m, 4m,
                    30m, 3m, 10m, 1m, 300m, 30m, 200m, 20m)),
        };

        var band = CutTotalsBandCalculator.Compute(periods);

        band.TotalPositive.ShouldBe(new BandValue(200m, 100m, 300m));
        band.TotalPositiveAlt.ShouldBe(new BandValue(20m, 10m, 30m));
        band.TotalNet.ShouldBe(new BandValue(100m, 0m, 200m));
        band.TotalDeudaEnCurso.ShouldBe(new BandValue(60m, 60m, 60m));
    }

    // ── DASH-3: 0/1 period edge cases (backend math must not throw) ────────────

    [Fact]
    public void Compute_NoPeriods_ReturnsZeroBand()
    {
        var band = CutTotalsBandCalculator.Compute(Array.Empty<PeriodAverageDto>());

        band.ShouldBe(TotalsBandDto.Zero);
    }

    [Fact]
    public void Compute_SinglePeriod_AvgEqualsMinEqualsMax()
    {
        var periods = new[] { PeriodWith(periodAvg: 200m) };

        var band = CutTotalsBandCalculator.Compute(periods);

        band.TotalPositive.Avg.ShouldBe(200m);
        band.TotalPositive.Min.ShouldBe(200m);
        band.TotalPositive.Max.ShouldBe(200m);
    }

    private static PeriodAverageDto PeriodWith(decimal periodAvg) =>
        new(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            new ConceptTotalsDto(
                periodAvg, periodAvg, periodAvg, periodAvg,
                periodAvg, periodAvg, periodAvg, periodAvg,
                periodAvg, periodAvg, periodAvg, periodAvg,
                periodAvg, periodAvg, periodAvg, periodAvg));
}
