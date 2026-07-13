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
        Guid    BudgetLineId,
        string  BudgetLineName,
        decimal TotalExpenses,
        decimal TotalCreditNotes,
        decimal TotalDebitNotes,
        decimal NetTotal);

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
}
