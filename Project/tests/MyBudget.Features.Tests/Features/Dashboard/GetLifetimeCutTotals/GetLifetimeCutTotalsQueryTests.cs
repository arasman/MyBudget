using MyBudget.Features.Features.Dashboard.GetLifetimeCutTotals;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Dashboard.GetLifetimeCutTotals;

/// <summary>
/// Unit tests for GetLifetimeCutTotals response model (DASH-1).
/// SQL-level correctness (CutDate ordering, empty-budget shape, BudgetId scoping)
/// is covered by integration tests in MyBudget.Integration.Tests.
/// </summary>
public sealed class GetLifetimeCutTotalsQueryTests
{
    // ── DTO structure tests ───────────────────────────────────────────────────

    [Fact]
    public void CutTotalsPointDto_Holds16TotalsPlusCutDateAndExchangeRate()
    {
        var cutDate = new DateOnly(2026, 7, 28);

        var dto = new CutTotalsPointDto(
            CutDate:              cutDate,
            ExchangeRate:         7.8m,
            TotalPositive:        500m, TotalPositiveAlt:        64.10m,
            TotalNegative:        200m, TotalNegativeAlt:        25.64m,
            TotalDeudaEnCurso:    500m, TotalDeudaEnCursoAlt:    64.10m,
            TotalBudgeted:        400m, TotalBudgetedAlt:        51.28m,
            TotalRegistered:      300m, TotalRegisteredAlt:      38.46m,
            Remaining:            100m, RemainingAlt:            12.82m,
            TotalAvailable:       500m, TotalAvailableAlt:       64.10m,
            TotalNet:             0m,   TotalNetAlt:             0m);

        dto.CutDate.ShouldBe(cutDate);
        dto.ExchangeRate.ShouldBe(7.8m);
        dto.TotalPositive.ShouldBe(500m);
        dto.TotalPositiveAlt.ShouldBe(64.10m);
        dto.TotalNet.ShouldBe(0m);
        dto.TotalNetAlt.ShouldBe(0m);
    }

    [Fact]
    public void LifetimeCutTotalsResponse_CarriesConversionBasisAndPoints()
    {
        var point = new CutTotalsPointDto(
            new DateOnly(2026, 1, 15), 7.8m,
            500m, 64.10m, 200m, 25.64m, 500m, 64.10m,
            400m, 51.28m, 300m, 38.46m, 100m, 12.82m,
            500m, 64.10m, 0m, 0m);

        var response = new LifetimeCutTotalsResponse(
            ConversionBasis: "cut-frozen",
            Points:          new List<CutTotalsPointDto> { point });

        response.ConversionBasis.ShouldBe("cut-frozen");
        response.Points.ShouldHaveSingleItem();
        response.Points[0].CutDate.ShouldBe(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public void LifetimeCutTotalsResponse_EmptyPoints_RepresentsNoCutsBudget()
    {
        // DASH-1 scenario: budget with zero CutRecords -> 200 OK with an empty series.
        var response = new LifetimeCutTotalsResponse(
            ConversionBasis: "cut-frozen",
            Points:          new List<CutTotalsPointDto>());

        response.Points.ShouldBeEmpty();
    }
}
