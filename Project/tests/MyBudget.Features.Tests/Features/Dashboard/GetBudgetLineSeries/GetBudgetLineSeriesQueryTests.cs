using MyBudget.Features.Features.Dashboard.GetBudgetLineSeries;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Dashboard.GetBudgetLineSeries;

/// <summary>
/// Unit tests for the GetBudgetLineSeries response model shape (DASH-4/5/6/12).
/// SQL-level correctness (cross-cycle BudgetLineId matching, ANY(@lineIds/@periodIds)
/// filtering, net-total formula) is covered by integration tests.
/// </summary>
public sealed class GetBudgetLineSeriesQueryTests
{
    [Fact]
    public void BudgetLineSeriesResponse_CarriesConversionBasisPeriodsAndRows()
    {
        var periodId = Guid.NewGuid();
        var cycleId  = Guid.NewGuid();
        var lineId   = Guid.NewGuid();
        var currencyId = Guid.NewGuid();

        var period = new PeriodSeriesDto(periodId, cycleId, new DateOnly(2026, 1, 1), currencyId);
        var row    = new BudgetLineSeriesRowDto(lineId, "Rent", periodId, 1500m, 1400m);

        var response = new BudgetLineSeriesResponse(
            ConversionBasis: "transaction-time",
            Periods:         new List<PeriodSeriesDto> { period },
            Rows:            new List<BudgetLineSeriesRowDto> { row });

        response.ConversionBasis.ShouldBe("transaction-time");
        response.Periods.ShouldHaveSingleItem();
        response.Periods[0].CycleId.ShouldBe(cycleId);
        response.Periods[0].DefaultCurrencyId.ShouldBe(currencyId);
        response.Rows.ShouldHaveSingleItem();
        response.Rows[0].BudgetLineId.ShouldBe(lineId);
        response.Rows[0].NetTotal.ShouldBe(1400m);
    }

    [Fact]
    public void BudgetLineSeriesResponse_NoMatchingLinesOrPeriods_ReturnsEmptyRows()
    {
        var response = new BudgetLineSeriesResponse(
            ConversionBasis: "transaction-time",
            Periods:         new List<PeriodSeriesDto>(),
            Rows:            new List<BudgetLineSeriesRowDto>());

        response.Periods.ShouldBeEmpty();
        response.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void PeriodSeriesDto_CarriesCycleScopedDefaultCurrencyId()
    {
        // DASH-12: DefaultCurrencyId lives on Cycle, not Budget — carried per period so
        // the client can detect a cross-cycle currency mismatch.
        var usd = Guid.NewGuid();
        var eur = Guid.NewGuid();

        var periodUsd = new PeriodSeriesDto(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), usd);
        var periodEur = new PeriodSeriesDto(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 2, 1), eur);

        periodUsd.DefaultCurrencyId.ShouldNotBe(periodEur.DefaultCurrencyId);
    }

    [Fact]
    public void GetBudgetLineSeriesQuery_CarriesBudgetIdLineIdsAndPeriodIds()
    {
        var budgetId  = Guid.NewGuid();
        var lineIds   = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var periodIds = new[] { Guid.NewGuid() };

        var query = new GetBudgetLineSeriesQuery(budgetId, lineIds, periodIds);

        query.BudgetId.ShouldBe(budgetId);
        query.LineIds.ShouldBe(lineIds);
        query.PeriodIds.ShouldBe(periodIds);
    }
}
