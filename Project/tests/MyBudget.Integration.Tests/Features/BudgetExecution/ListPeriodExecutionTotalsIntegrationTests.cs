using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for ListPeriodExecutionTotals endpoint.
/// Covers REQ-EXEC-TOTALS-1 through REQ-EXEC-TOTALS-4.
/// </summary>
public sealed class ListPeriodExecutionTotalsIntegrationTests : BudgetExecutionTestBase
{
    public ListPeriodExecutionTotalsIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record TotalsResponse(
        List<LineTotalDto>     LineTotals,
        List<CategoryTotalDto> CategoryTotals);

    private sealed record LineTotalDto(
        Guid     BudgetLineId,
        string   BudgetLineName,
        decimal  BudgetedAmount,
        decimal  TotalExpenses,
        decimal  TotalCreditNotes,
        decimal  TotalDebitNotes,
        decimal  NetTotal);

    private sealed record CategoryTotalDto(
        Guid    CategoryGroupId,
        string  CategoryGroupName,
        Guid?   CategoryId,
        string? CategoryName,
        decimal TotalExpenses,
        decimal TotalCreditNotes,
        decimal TotalDebitNotes,
        decimal NetTotal);

    // ── REQ-EXEC-TOTALS-1: Dual shape returned ───────────────────────────────

    [Fact]
    public async Task GetTotals_ReturnsBothShapes()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-totals1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m, entryType: 1);

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.LineTotals.ShouldNotBeNull();
        body.CategoryTotals.ShouldNotBeNull();
    }

    // ── REQ-EXEC-TOTALS-2: netAmount formula ─────────────────────────────────

    [Fact]
    public async Task GetTotals_NetAmountFormula_ExpensePlusDebitMinusCredit()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-totals2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        // Expense=100, Expense=50, CreditNote=30, DebitNote=20
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m, entryType: 1);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 50m,  entryType: 1);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 30m,  entryType: 2, note: "credit");
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 20m,  entryType: 3, note: "debit");

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        body.ShouldNotBeNull();

        var lineTotal = body!.LineTotals.FirstOrDefault(l => l.BudgetLineId == lineId);
        lineTotal.ShouldNotBeNull();
        lineTotal!.TotalExpenses.ShouldBe(150m);
        lineTotal.TotalCreditNotes.ShouldBe(30m);
        lineTotal.TotalDebitNotes.ShouldBe(20m);
        // netAmount = 150 + 20 - 30 = 140
        lineTotal.NetTotal.ShouldBe(140m);
    }

    // ── REQ-EXEC-TOTALS-3: Per-category aggregation ──────────────────────────

    [Fact]
    public async Task GetTotals_CategoryAggregation_GroupsByCategoryGroupId()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-totals3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var line1Id       = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Rent");
        var line2Id       = await CreateBudgetLineAsync(budgetId, periodId, groupId, "Utilities");

        await CreateExecutionRecordAsync(budgetId, periodId, line1Id, 50m);
        await CreateExecutionRecordAsync(budgetId, periodId, line2Id, 50m);

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        body.ShouldNotBeNull();

        // Both lines share the same CategoryGroupId → one category entry with NetTotal=100
        var catTotal = body!.CategoryTotals.FirstOrDefault(c => c.CategoryGroupId == groupId);
        catTotal.ShouldNotBeNull();
        catTotal!.NetTotal.ShouldBe(100m);
    }

    // ── REQ-EXEC-TOTALS-1 (date-range): Line active for period shows BudgetedAmount ─

    [Fact]
    public async Task GetTotals_LineActiveForPeriod_ShowsBudgetedAmount()
    {
        // REQ-EXEC-TOTALS-1: a BudgetLine whose date range intersects the period
        // appears in LineTotals with its effective BudgetedAmount.
        var (_, budgetId) = await SetupOwnerAsync("exec-totals-active@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        // Line covers period: startDate=2025-01-01 (before period), no endDate
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId,
            name: "ActiveLine", amount: 1500m, startDate: new DateOnly(2025, 1, 1));

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        var lineTotal = body!.LineTotals.FirstOrDefault(l => l.BudgetLineId == lineId);
        lineTotal.ShouldNotBeNull("Line active for the period should appear in LineTotals");
        lineTotal!.BudgetedAmount.ShouldBe(1500m);
    }

    [Fact]
    public async Task GetTotals_LineInactiveForPeriod_NotIncluded()
    {
        // REQ-EXEC-TOTALS-1: a BudgetLine whose date range does NOT intersect the period
        // must NOT appear in LineTotals.
        var (_, budgetId) = await SetupOwnerAsync("exec-totals-inactive@example.com");
        var cycleId       = await CreateCycleAsync(budgetId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 12, 31));
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        // Line starts AFTER the period ends — should not appear in Jan period totals
        var lineId = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId,
            name: "FutureLine", amount: 999m, startDate: new DateOnly(2025, 2, 1));

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        body!.LineTotals.ShouldNotContain(l => l.BudgetLineId == lineId,
            "Line that starts after period end should not appear in totals");
    }

    [Fact]
    public async Task GetTotals_LineWithSplitRevision_UsesCorrectAmountForPeriod()
    {
        // REQ-EXEC-TOTALS-1: the effective revision selected is the one where
        // ValidFrom <= PeriodStart AND (ValidTo IS NULL OR ValidTo >= PeriodStart).
        // A split revision that starts after PeriodStart should NOT be used.
        var (_, budgetId) = await SetupOwnerAsync("exec-totals-split@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId,
            name: "SplitLine", amount: 1000m, startDate: new DateOnly(2025, 1, 1));

        // Perform a revision split with ValidFrom=today so it doesn't affect Jan 2025 period
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}",
            new { name = "SplitLine", lineType = "Expense", categoryGroupId = groupId,
                  validFrom = today, budgetedAmount = 2000m,
                  currencyId = MyBudget.Features.SharedKernel.Entities.CurrencySeeds.GtqId });

        // The Jan 2025 period should still see the original 1000 amount
        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/execution-totals");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TotalsResponse>(JsonOpts);
        var lineTotal = body!.LineTotals.FirstOrDefault(l => l.BudgetLineId == lineId);
        lineTotal.ShouldNotBeNull();
        lineTotal!.BudgetedAmount.ShouldBe(1000m,
            "Period in Jan 2025 should use original revision (1000), not the split (2000)");
    }
}
