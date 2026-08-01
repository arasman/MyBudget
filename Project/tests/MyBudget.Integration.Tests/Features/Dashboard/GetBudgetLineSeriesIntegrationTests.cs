using System.Net;
using System.Net.Http.Json;
using System.Web;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Dashboard;

/// <summary>
/// Integration tests for GetBudgetLineSeries endpoint (DASH-4, DASH-5, DASH-6, DASH-12,
/// role matrix DASH-8).
/// </summary>
public sealed class GetBudgetLineSeriesIntegrationTests : DashboardTestBase
{
    public GetBudgetLineSeriesIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record BudgetLineSeriesResponse(
        string                    ConversionBasis,
        List<PeriodSeriesDto>    Periods,
        List<BudgetLineSeriesRowDto> Rows);

    private sealed record PeriodSeriesDto(
        Guid     PeriodId,
        Guid     CycleId,
        DateOnly PeriodStart,
        Guid     DefaultCurrencyId);

    private sealed record BudgetLineSeriesRowDto(
        Guid    BudgetLineId,
        string  BudgetLineName,
        Guid    PeriodId,
        decimal BudgetedAmount,
        decimal NetTotal);

    private static string BuildUrl(Guid budgetId, IEnumerable<Guid> lineIds, IEnumerable<Guid> periodIds)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var lineId in lineIds) query.Add("lineIds", lineId.ToString());
        foreach (var periodId in periodIds) query.Add("periodIds", periodId.ToString());

        return $"/api/budgets/{budgetId}/dashboard/line-series?{query}";
    }

    // ── DASH-4: cross-cycle series by BudgetLineId identity ─────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_CrossCycleByBudgetLineId_ReturnsOneContinuousSeries()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-crosscycle@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var cycle1Id  = await CreateCycleAsync(budgetId, "Cycle 1", start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var period1Id = await CreatePeriodAsync(budgetId, cycle1Id, "January 2025", 1,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));

        // Line created before Cycle 1 so it covers both cycles' date ranges.
        var lineId = await CreateBudgetLineAsync(budgetId, period1Id, groupId, "Rent",
            startDate: new DateOnly(2020, 1, 1));

        // A Cycle is created with IsActive=false by default (only one Cycle may be active
        // per budget) — creating a second, non-overlapping Cycle needs no activation for
        // this read-only series query.
        var cycle2Id = await CreateCycleAsync(budgetId, "Cycle 2", start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31));
        var period2Id = await CreatePeriodAsync(budgetId, cycle2Id, "January 2026", 1,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));

        await CreateExecutionRecordAsync(budgetId, period1Id, lineId, 100m, operationDate: new DateOnly(2025, 1, 15));
        await CreateExecutionRecordAsync(budgetId, period2Id, lineId, 200m, operationDate: new DateOnly(2026, 1, 15));

        var url = BuildUrl(budgetId, [lineId], [period1Id, period2Id]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.ConversionBasis.ShouldBe("transaction-time");
        body.Periods.Count.ShouldBe(2);
        body.Rows.Count.ShouldBe(2);
        body.Rows.ShouldAllBe(r => r.BudgetLineId == lineId);
        body.Rows.Single(r => r.PeriodId == period1Id).NetTotal.ShouldBe(100m);
        body.Rows.Single(r => r.PeriodId == period2Id).NetTotal.ShouldBe(200m);
    }

    // ── DASH-5: period-vs-period within a cycle ─────────────────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_PeriodVsPeriodWithinCycle_ReturnsBothPeriodsSideBySide()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-periodvsperiod@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId, "Groceries",
            startDate: new DateOnly(2025, 1, 1));

        var period1Id = await CreatePeriodAsync(budgetId, cycleId, "January", 1,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var period2Id = await CreatePeriodAsync(budgetId, cycleId, "February", 2,
            start: new DateOnly(2025, 2, 1), end: new DateOnly(2025, 2, 28));

        await CreateExecutionRecordAsync(budgetId, period1Id, lineId, 300m, operationDate: new DateOnly(2025, 1, 10));
        await CreateExecutionRecordAsync(budgetId, period2Id, lineId, 450m, operationDate: new DateOnly(2025, 2, 10));

        var url = BuildUrl(budgetId, [lineId], [period1Id, period2Id]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.Periods.ShouldAllBe(p => p.CycleId == cycleId);
        body.Rows.Single(r => r.PeriodId == period1Id).NetTotal.ShouldBe(300m);
        body.Rows.Single(r => r.PeriodId == period2Id).NetTotal.ShouldBe(450m);
    }

    // ── DASH-6 / DASH-12: cycle-vs-cycle with mismatched currencies ─────────────

    [Fact]
    public async Task GetBudgetLineSeries_CycleVsCycle_PeriodsCarryDistinctCycleDefaultCurrencyIds()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-cyclevscycle@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId, "Utilities",
            startDate: new DateOnly(2020, 1, 1));

        var usdCycleId = await CreateCycleAsync(budgetId, "USD Cycle",
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31), defaultCurrencyId: CurrencySeeds.UsdId);
        var usdPeriodId = await CreatePeriodAsync(budgetId, usdCycleId, "USD Period", 1,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));

        var eurCycleId = await CreateCycleAsync(budgetId, "EUR Cycle",
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 12, 31), defaultCurrencyId: CurrencySeeds.EurId);
        var eurPeriodId = await CreatePeriodAsync(budgetId, eurCycleId, "EUR Period", 1,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));

        await CreateExecutionRecordAsync(budgetId, usdPeriodId, lineId, 100m,
            currencyId: CurrencySeeds.UsdId, operationDate: new DateOnly(2025, 1, 15));
        await CreateExecutionRecordAsync(budgetId, eurPeriodId, lineId, 100m,
            currencyId: CurrencySeeds.EurId, operationDate: new DateOnly(2026, 1, 15));

        var url = BuildUrl(budgetId, [lineId], [usdPeriodId, eurPeriodId]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.Periods.Count.ShouldBe(2);

        var usdPeriod = body.Periods.Single(p => p.PeriodId == usdPeriodId);
        var eurPeriod = body.Periods.Single(p => p.PeriodId == eurPeriodId);
        usdPeriod.DefaultCurrencyId.ShouldBe(CurrencySeeds.UsdId);
        eurPeriod.DefaultCurrencyId.ShouldBe(CurrencySeeds.EurId);
        usdPeriod.DefaultCurrencyId.ShouldNotBe(eurPeriod.DefaultCurrencyId);
    }

    // ── Net total formula: Expense + DebitNote - CreditNote (currency conversion) ────

    [Fact]
    public async Task GetBudgetLineSeries_NetTotalFormula_ExpensePlusDebitMinusCredit()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-netformula@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Insurance",
            startDate: new DateOnly(2025, 1, 1));

        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m, entryType: 1); // Expense
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 50m,  entryType: 1); // Expense
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 30m,  entryType: 2); // CreditNote
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 20m,  entryType: 3); // DebitNote

        var url = BuildUrl(budgetId, [lineId], [periodId]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        var row = body!.Rows.Single(r => r.BudgetLineId == lineId && r.PeriodId == periodId);
        // 150 + 20 - 30 = 140
        row.NetTotal.ShouldBe(140m);
    }

    // ── BudgetedAmount via effective revision ────────────────────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_ReturnsEffectiveBudgetedAmountForPeriod()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-budgeted@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Subscriptions",
            amount: 250m, startDate: new DateOnly(2025, 1, 1));

        var url = BuildUrl(budgetId, [lineId], [periodId]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        var row = body!.Rows.Single(r => r.BudgetLineId == lineId && r.PeriodId == periodId);
        row.BudgetedAmount.ShouldBe(250m);
        row.NetTotal.ShouldBe(0m);
    }

    // ── Multiple selected lines ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_MultipleLineIds_ReturnsRowsForEachSelectedLine()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-multiline@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var line1Id       = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Rent", startDate: new DateOnly(2025, 1, 1));
        var line2Id       = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Utilities", startDate: new DateOnly(2025, 1, 1));

        await CreateExecutionRecordAsync(budgetId, periodId, line1Id, 500m);
        await CreateExecutionRecordAsync(budgetId, periodId, line2Id, 75m);

        var url = BuildUrl(budgetId, [line1Id, line2Id], [periodId]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body!.Rows.Count.ShouldBe(2);
        body.Rows.Single(r => r.BudgetLineId == line1Id).NetTotal.ShouldBe(500m);
        body.Rows.Single(r => r.BudgetLineId == line2Id).NetTotal.ShouldBe(75m);
    }

    // ── BudgetId scoping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_ScopedToBudgetId_ExcludesOtherBudgetsLines()
    {
        var (_, budgetIdA) = await SetupOwnerAsync("dash-line-scopeA@example.com");
        var cycleAId  = await CreateCycleAsync(budgetIdA, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodAId = await CreatePeriodAsync(budgetIdA, cycleAId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupAId  = await CreateCategoryGroupAsync(budgetIdA);
        var lineAId   = await CreateBudgetLineAsync(budgetIdA, periodAId, groupAId, "Line A", startDate: new DateOnly(2025, 1, 1));
        await CreateExecutionRecordAsync(budgetIdA, periodAId, lineAId, 111m);

        var (ownerBToken, budgetIdB) = await SetupOwnerAsync("dash-line-scopeB@example.com");
        var cycleBId  = await CreateCycleAsync(budgetIdB, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodBId = await CreatePeriodAsync(budgetIdB, cycleBId, start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupBId  = await CreateCategoryGroupAsync(budgetIdB);
        var lineBId   = await CreateBudgetLineAsync(budgetIdB, periodBId, groupBId, "Line B", startDate: new DateOnly(2025, 1, 1));
        await CreateExecutionRecordAsync(budgetIdB, periodBId, lineBId, 222m);

        AuthorizeClient(ownerBToken);
        // Attempt to reach across into budget A's line/period from budget B's route.
        var url = BuildUrl(budgetIdB, [lineAId, lineBId], [periodAId, periodBId]);
        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body!.Periods.ShouldHaveSingleItem();
        body.Periods[0].PeriodId.ShouldBe(periodBId);
        body.Rows.ShouldHaveSingleItem();
        body.Rows[0].BudgetLineId.ShouldBe(lineBId);
        body.Rows[0].NetTotal.ShouldBe(222m);
    }

    // ── Empty selection ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBudgetLineSeries_NoLineIdsOrPeriodIdsProvided_ReturnsEmptyShape()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-empty@example.com");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/line-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetLineSeriesResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.ConversionBasis.ShouldBe("transaction-time");
        body.Periods.ShouldBeEmpty();
        body.Rows.ShouldBeEmpty();
    }

    // ── DASH-8: role-gating matrix (extends coverage to this 3rd endpoint) ──────

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("read-only")]
    public async Task GetBudgetLineSeries_AllFourRoles_Return200(string role)
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync($"dash-line-role-{role}-owner@example.com");

        var token = role switch
        {
            "owner"     => ownerToken,
            "admin"     => await SetupAdminAsync(budgetId, $"dash-line-role-{role}-member@example.com"),
            "operator"  => await SetupOperatorAsync(budgetId, $"dash-line-role-{role}-member@example.com"),
            "read-only" => await SetupViewerAsync(budgetId, $"dash-line-role-{role}-member@example.com"),
            _           => throw new ArgumentOutOfRangeException(nameof(role)),
        };
        AuthorizeClient(token);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/line-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBudgetLineSeries_NoRoleOnBudget_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-line-role-noaccess-owner@example.com");
        var outsiderLogin = await RegisterUserAsync("dash-line-role-noaccess-outsider@example.com");
        AuthorizeClient(outsiderLogin.AccessToken);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/line-series");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
