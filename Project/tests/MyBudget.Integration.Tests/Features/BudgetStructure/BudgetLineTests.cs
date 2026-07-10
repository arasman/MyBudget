using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for BudgetLine endpoints.
/// Covers REQ-BL-01 to REQ-BL-04.
/// </summary>
public sealed class BudgetLineTests : BudgetStructureTestBase
{
    public BudgetLineTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-BL-02: Create BudgetLine ─────────────────────────────────────────

    [Fact]
    public async Task CreateBudgetLine_HappyPathWithCategory_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var catId         = await CreateCategoryAsync(budgetId, groupId, "Rent");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                isRecurring     = true,
                categoryGroupId = groupId,
                categoryId      = catId,
                budgetedAmount  = 1500m,
                currency        = "GTQ",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateBudgetLine_HappyPathWithoutCategory_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name            = "Miscellaneous",
                lineType        = "Expense",
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 500m,
                currency        = "USD",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBudgetLine_ClosedPeriod_Returns409WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        // Close the period
        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 1500m,
                currency        = "GTQ",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task CreateBudgetLine_InvalidLineType_Returns400()
    {
        // "Income" is not a valid LineType enum value.
        // JsonStringEnumConverter rejects unknown enum names at deserialization → 400 BadRequest.
        var (_, budgetId) = await SetupOwnerAsync("bl-create4@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name            = "Salary",
                lineType        = "Income",  // not a valid enum member
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 5000m,
                currency        = "GTQ",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBudgetLine_InvalidCurrency_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-create5@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 1500m,
                currency        = "EUR",  // invalid — only GTQ and USD allowed
            });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ── REQ-BL-03: Update BudgetLine ─────────────────────────────────────────

    [Fact]
    public async Task UpdateBudgetLine_HappyPath_CreatesNewRevision_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-update1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId, amount: 1500m);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}",
            new
            {
                name            = "Rent Updated",
                lineType        = "Expense",
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 2000m,
                currency        = "GTQ",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify latest revision shows 2000 via read endpoint
        var linesResponse = await Client.GetAsync($"/api/budgets/{budgetId}/periods/{periodId}/lines");
        var lines = await linesResponse.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.Single().BudgetedAmount.ShouldBe(2000m);
    }

    [Fact]
    public async Task UpdateBudgetLine_ClosedPeriod_Returns409()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-update2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                isRecurring     = false,
                categoryGroupId = groupId,
                budgetedAmount  = 1600m,
                currency        = "GTQ",
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
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify line is gone from list
        var linesResponse = await Client.GetAsync($"/api/budgets/{budgetId}/periods/{periodId}/lines");
        var lines = await linesResponse.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteBudgetLine_ClosedPeriod_Returns409()
    {
        var (_, budgetId) = await SetupOwnerAsync("bl-delete2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_CLOSED");
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
    private sealed record BudgetLineItem(
        Guid     Id,
        string   Name,
        string   LineType,
        bool     IsRecurring,
        Guid     CategoryGroupId,
        Guid?    CategoryId,
        decimal? BudgetedAmount,
        string?  Currency);
}
