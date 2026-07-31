using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Dashboard;

/// <summary>
/// Integration tests for GetCutTotalsBand endpoint (DASH-2, DASH-3, DASH-11).
/// </summary>
public sealed class GetCutTotalsBandIntegrationTests : DashboardTestBase
{
    public GetCutTotalsBandIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record CutTotalsBandResponse(
        string                  ConversionBasis,
        int                     PeriodCount,
        List<PeriodAverageDto> Periods,
        TotalsBandDto           Band);

    private sealed record PeriodAverageDto(
        Guid             PeriodId,
        DateOnly         PeriodStart,
        DateOnly         PeriodEnd,
        ConceptTotalsDto Avg);

    private sealed record ConceptTotalsDto(
        decimal TotalPositive,      decimal TotalPositiveAlt,
        decimal TotalNegative,      decimal TotalNegativeAlt,
        decimal TotalDeudaEnCurso,  decimal TotalDeudaEnCursoAlt,
        decimal TotalBudgeted,      decimal TotalBudgetedAlt,
        decimal TotalRegistered,    decimal TotalRegisteredAlt,
        decimal Remaining,          decimal RemainingAlt,
        decimal TotalAvailable,     decimal TotalAvailableAlt,
        decimal TotalNet,           decimal TotalNetAlt);

    private sealed record BandValue(decimal Avg, decimal Min, decimal Max);

    private sealed record TotalsBandDto(
        BandValue TotalPositive,      BandValue TotalPositiveAlt,
        BandValue TotalNegative,      BandValue TotalNegativeAlt,
        BandValue TotalDeudaEnCurso,  BandValue TotalDeudaEnCursoAlt,
        BandValue TotalBudgeted,      BandValue TotalBudgetedAlt,
        BandValue TotalRegistered,    BandValue TotalRegisteredAlt,
        BandValue Remaining,          BandValue RemainingAlt,
        BandValue TotalAvailable,     BandValue TotalAvailableAlt,
        BandValue TotalNet,           BandValue TotalNetAlt);

    private sealed record LifetimeCutTotalsResponse(string ConversionBasis, List<CutTotalsPointDto> Points);

    private sealed record CutTotalsPointDto(DateOnly CutDate, decimal ExchangeRate, decimal TotalPositive);

    // ── DASH-2: two-stage period averaging (exact spec scenario) ───────────────

    [Fact]
    public async Task GetCutTotalsBand_TwoPeriodsWithCuts_ReturnsAvgMinMaxOfPeriodAverages()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-band-twoperiods@example.com");
        var cycleId = await CreateCycleAsync(budgetId, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));

        var periodAId = await CreatePeriodAsync(
            budgetId, cycleId, name: "Period A", periodNumber: 1,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        var periodBId = await CreatePeriodAsync(
            budgetId, cycleId, name: "Period B", periodNumber: 2,
            start: new DateOnly(2026, 2, 1), end: new DateOnly(2026, 2, 28));

        // Period A: cuts totaling [100, 200] -> period avg 150.
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 1, 5),  marker: 100m);
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 1, 20), marker: 200m);
        // Period B: one cut totaling 300 -> period avg 300.
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 2, 10), marker: 300m);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-band");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CutTotalsBandResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.ConversionBasis.ShouldBe("cut-frozen");
        body.PeriodCount.ShouldBe(2);
        body.Periods.Count.ShouldBe(2);

        var periodA = body.Periods.Single(p => p.PeriodId == periodAId);
        periodA.Avg.TotalPositive.ShouldBe(150m);
        var periodB = body.Periods.Single(p => p.PeriodId == periodBId);
        periodB.Avg.TotalPositive.ShouldBe(300m);

        // Not the flat average of [100, 200, 300] (200) — the period-averaged band.
        body.Band.TotalPositive.Avg.ShouldBe(225m);
        body.Band.TotalPositive.Min.ShouldBe(150m);
        body.Band.TotalPositive.Max.ShouldBe(300m);
    }

    // ── DASH-11: date-containment exclusion, band-only (lifetime series unaffected) ────

    [Fact]
    public async Task GetCutTotalsBand_CutOutsideAllPeriods_ExcludedFromBandButKeptInLifetimeSeries()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-band-outsideperiod@example.com");
        var cycleId = await CreateCycleAsync(budgetId, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));
        var periodId = await CreatePeriodAsync(
            budgetId, cycleId, name: "January", periodNumber: 1,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));

        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 1, 15), marker: 100m); // inside Period
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 6, 1),  marker: 999m); // outside every Period

        var bandResponse = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-band");
        bandResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var band = await bandResponse.Content.ReadFromJsonAsync<CutTotalsBandResponse>(JsonOpts);
        band.ShouldNotBeNull();
        band!.PeriodCount.ShouldBe(1);
        band.Periods.ShouldHaveSingleItem();
        band.Periods[0].PeriodId.ShouldBe(periodId);
        band.Band.TotalPositive.Avg.ShouldBe(100m);
        band.Band.TotalPositive.Min.ShouldBe(100m);
        band.Band.TotalPositive.Max.ShouldBe(100m);

        var lifetimeResponse = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-series");
        lifetimeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lifetime = await lifetimeResponse.Content.ReadFromJsonAsync<LifetimeCutTotalsResponse>(JsonOpts);
        lifetime.ShouldNotBeNull();
        lifetime!.Points.Count.ShouldBe(2); // both cuts still present in the lifetime series
        lifetime.Points.ShouldContain(p => p.TotalPositive == 999m);
    }

    // ── DASH-3: periodCount 0/1 -> backend must not error, client renders empty state ──

    [Fact]
    public async Task GetCutTotalsBand_NoCuts_ReturnsPeriodCountZero()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-band-empty@example.com");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-band");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CutTotalsBandResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.PeriodCount.ShouldBe(0);
        body.Periods.ShouldBeEmpty();
        body.ConversionBasis.ShouldBe("cut-frozen");
    }

    [Fact]
    public async Task GetCutTotalsBand_OneCutInOnePeriod_ReturnsPeriodCountOne()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-band-onecut@example.com");
        var cycleId  = await CreateCycleAsync(budgetId, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));
        var periodId = await CreatePeriodAsync(
            budgetId, cycleId, name: "January", periodNumber: 1,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 1, 10), marker: 200m);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-band");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CutTotalsBandResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.PeriodCount.ShouldBe(1);
        body.Periods.ShouldHaveSingleItem();
        body.Periods[0].PeriodId.ShouldBe(periodId);
        body.Band.TotalPositive.Avg.ShouldBe(200m);
        body.Band.TotalPositive.Min.ShouldBe(200m);
        body.Band.TotalPositive.Max.ShouldBe(200m);
    }

    // ── BudgetId scoping ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCutTotalsBand_ScopedToBudgetId_ExcludesOtherBudgetsCuts()
    {
        var (_, budgetIdA) = await SetupOwnerAsync("dash-band-scopeA@example.com");
        var cycleAId  = await CreateCycleAsync(budgetIdA, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));
        await CreatePeriodAsync(budgetIdA, cycleAId, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        await CreateCutRecordAsync(budgetIdA, new DateOnly(2026, 1, 5), marker: 111m);

        var (ownerBToken, budgetIdB) = await SetupOwnerAsync("dash-band-scopeB@example.com");
        var cycleBId  = await CreateCycleAsync(budgetIdB, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));
        await CreatePeriodAsync(budgetIdB, cycleBId, start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        await CreateCutRecordAsync(budgetIdB, new DateOnly(2026, 1, 5), marker: 222m);

        AuthorizeClient(ownerBToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetIdB}/dashboard/cut-totals-band");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CutTotalsBandResponse>(JsonOpts);
        body!.PeriodCount.ShouldBe(1);
        body.Band.TotalPositive.Avg.ShouldBe(222m);
    }
}
