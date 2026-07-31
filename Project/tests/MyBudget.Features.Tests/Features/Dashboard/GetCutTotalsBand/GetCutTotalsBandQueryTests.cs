using MyBudget.Features.Features.Dashboard.GetCutTotalsBand;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Dashboard.GetCutTotalsBand;

/// <summary>
/// Unit tests for the GetCutTotalsBand response model shape (DASH-2/DASH-3).
/// SQL-level correctness (date-containment join, DASH-11 exclusion, GROUP BY) is covered
/// by integration tests. Stage-2 aggregation math is covered by CutTotalsBandCalculatorTests.
/// </summary>
public sealed class GetCutTotalsBandQueryTests
{
    [Fact]
    public void CutTotalsBandResponse_CarriesConversionBasisPeriodCountPeriodsAndBand()
    {
        var period = new PeriodAverageDto(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
            new ConceptTotalsDto(
                150m, 15m, 50m, 5m, 60m, 6m, 40m, 4m,
                30m, 3m, 10m, 1m, 150m, 15m, 0m, 0m));

        var response = new CutTotalsBandResponse(
            ConversionBasis: "cut-frozen",
            PeriodCount:     1,
            Periods:         new List<PeriodAverageDto> { period },
            Band:            TotalsBandDto.Zero);

        response.ConversionBasis.ShouldBe("cut-frozen");
        response.PeriodCount.ShouldBe(1);
        response.Periods.ShouldHaveSingleItem();
        response.Periods[0].Avg.TotalPositive.ShouldBe(150m);
    }

    [Fact]
    public void CutTotalsBandResponse_ZeroPeriods_RepresentsInsufficientHistory()
    {
        // DASH-3 backend contract: periodCount 0 or 1 -> client renders the empty state.
        var response = new CutTotalsBandResponse(
            ConversionBasis: "cut-frozen",
            PeriodCount:     0,
            Periods:         new List<PeriodAverageDto>(),
            Band:            TotalsBandDto.Zero);

        response.PeriodCount.ShouldBe(0);
        response.Periods.ShouldBeEmpty();
        response.Band.ShouldBe(TotalsBandDto.Zero);
    }

    [Fact]
    public void BandValue_HoldsAvgMinMax()
    {
        var value = new BandValue(Avg: 225m, Min: 150m, Max: 300m);

        value.Avg.ShouldBe(225m);
        value.Min.ShouldBe(150m);
        value.Max.ShouldBe(300m);
    }
}
