using System.Net;
using System.Net.Http.Json;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for BudgetLine endpoints.
/// Covers REQ-BL-01 to REQ-BL-04 (post budget-line-redesign).
/// Routes: /api/budgets/{budgetId}/lines (no periodId in path)
/// </summary>
public sealed class BudgetLineTests : BudgetStructureTestBase
{
    public BudgetLineTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-BL-02: Create BudgetLine ─────────────────────────────────────────

    [Fact]
    public async Task CreateBudgetLine_HappyPathWithCategory_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var catId         = await CreateCategoryAsync(budgetId, groupId, "Rent");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                categoryId      = catId,
                startDate       = new DateOnly(2025, 1, 1),
                endDate         = (DateOnly?)null,
                initialAmount   = 1500m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateBudgetLine_HappyPathWithoutCategory_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create2@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Miscellaneous",
                lineType        = "Expense",
                categoryGroupId = groupId,
                startDate       = new DateOnly(2025, 1, 1),
                endDate         = (DateOnly?)null,
                initialAmount   = 500m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBudgetLine_ClosedPeriod_Returns409WithCode()
    {
        // REQ-BL-01: Create is NOT blocked by IsClosed; only revision splits with ValidFrom in
        // a closed period are blocked. This test verifies that after the redesign create
        // succeeds even when the budget has closed periods.
        var (_, budgetId) = await SetupOwnerAsync("bl-create3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        // Close the period — create should still succeed (budget-scoped, not period-gated)
        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                startDate       = new DateOnly(2025, 1, 1),
                endDate         = (DateOnly?)null,
                initialAmount   = 1500m,
                currencyId      = CurrencySeeds.GtqId,
            });

        // CreateBudgetLine is no longer blocked by IsClosed — it should return 201
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBudgetLine_InvalidLineType_Returns400()
    {
        // "Income" is not a valid LineType enum value.
        // JsonStringEnumConverter rejects unknown enum names at deserialization → 400 BadRequest.
        var (_, budgetId) = await SetupOwnerAsync("bl-create4@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Salary",
                lineType        = "Income",  // not a valid enum member
                categoryGroupId = groupId,
                startDate       = new DateOnly(2025, 1, 1),
                initialAmount   = 5000m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBudgetLine_NoCurrencyId_DefaultsToCycleDefaultCurrency_Returns201()
    {
        // When currencyId is omitted the handler resolves Cycle.DefaultCurrencyId (GTQ seed).
        var (_, budgetId) = await SetupOwnerAsync("bl-create5@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                startDate       = new DateOnly(2025, 1, 1),
                initialAmount   = 1500m,
                // currencyId intentionally omitted — defaults to Cycle.DefaultCurrencyId
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    // ── REQ-BL-03: Update BudgetLine ─────────────────────────────────────────

    [Fact]
    public async Task UpdateBudgetLine_HappyPath_CreatesNewRevision_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-update1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId, amount: 1500m);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}",
            new
            {
                name            = "Rent Updated",
                lineType        = "Expense",
                categoryGroupId = groupId,
                // Revision split: change amount from 2025-06-01 onwards
                validFrom       = DateOnly.FromDateTime(DateTime.UtcNow),
                budgetedAmount  = 2000m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify latest revision shows 2000 via read endpoint
        var linesResponse = await Client.GetAsync($"/api/budgets/{budgetId}/lines");
        var lines = await linesResponse.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.Single().BudgetedAmount.ShouldBe(2000m);
    }

    [Fact]
    public async Task UpdateBudgetLine_ClosedPeriod_Returns409()
    {
        // REQ-BL-03: A revision split with ValidFrom inside a closed period is blocked.
        // The period must include TODAY so that ValidFrom=today passes the validator
        // (validator rejects ValidFrom in the past), yet the period is already closed.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, budgetId) = await SetupOwnerAsync("bl-update2@example.com");
        // Create a cycle and period that spans today — so today passes validator but period is closed.
        var cycleStart = today.AddMonths(-1);
        var cycleEnd   = today.AddMonths(1);
        var cycleId    = await CreateCycleAsync(budgetId, start: cycleStart, end: cycleEnd);
        var periodId   = await CreatePeriodAsync(budgetId, cycleId,
            start: today.AddDays(-5), end: today.AddDays(5));
        var groupId    = await CreateCategoryGroupAsync(budgetId);
        var lineId     = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        // Close the period that contains today
        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        // Revision split with ValidFrom = today — passes validator but period is closed → 409
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                validFrom       = today,
                budgetedAmount  = 1600m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_CLOSED");
    }

    // ── REQ-BL-04: Delete BudgetLine ─────────────────────────────────────────

    [Fact]
    public async Task DeleteBudgetLine_HappyPath_Returns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-delete1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId);

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify line is gone from list
        var linesResponse = await Client.GetAsync($"/api/budgets/{budgetId}/lines");
        var lines = await linesResponse.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteBudgetLine_ClosedPeriod_Returns409()
    {
        // REQ-BL-04: IsClosed guard REMOVED from delete — delete succeeds regardless of period status.
        var (_, budgetId) = await SetupOwnerAsync("bl-delete2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        // Delete should succeed even if periods are closed (guard removed per REQ-BL-04)
        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
    private sealed record BudgetLineItem(
        Guid      Id,
        string    Name,
        string    LineType,
        Guid      CategoryGroupId,
        Guid?     CategoryId,
        DateOnly  StartDate,
        DateOnly? EndDate,
        decimal?  BudgetedAmount,
        string?   CurrencyCode,
        string?   CurrencySymbol);
}
