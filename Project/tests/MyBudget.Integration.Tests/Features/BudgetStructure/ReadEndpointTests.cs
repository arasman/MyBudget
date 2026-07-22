using System.Net;
using System.Net.Http.Json;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for read endpoints.
/// Covers REQ-READ-01 to REQ-READ-04.
/// </summary>
public sealed class ReadEndpointTests : BudgetStructureTestBase
{
    public ReadEndpointTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-READ-01: List Cycles ──────────────────────────────────────────────

    [Fact]
    public async Task ListCycles_ReturnsBothCycles_WithActiveFlagCorrect()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-cycles1@example.com");
        var c1 = await CreateCycleAsync(budgetId, "First Half", new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));
        var c2 = await CreateCycleAsync(budgetId, "Second Half", new DateOnly(2025, 7, 1), new DateOnly(2025, 12, 31));

        // Activate first cycle
        await Client.PutAsJsonAsync($"/api/budgets/{budgetId}/active-cycle", new { cycleId = c1 });

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<CycleListItem[]>(JsonOpts);
        list!.Length.ShouldBe(2);
        list.Single(c => c.Id == c1).IsActive.ShouldBeTrue();
        list.Single(c => c.Id == c2).IsActive.ShouldBeFalse();
    }

    // ── REQ-READ-02: Get Cycle Detail ─────────────────────────────────────────

    [Fact]
    public async Task GetCycleDetail_WithPeriods_ReturnsNestedPeriods()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-cycle-detail1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        await CreatePeriodAsync(budgetId, cycleId, "Jan", 1, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));
        await CreatePeriodAsync(budgetId, cycleId, "Feb", 2, new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 28));
        await CreatePeriodAsync(budgetId, cycleId, "Mar", 3, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles/{cycleId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CycleDetailResponse>(JsonOpts);
        body!.Id.ShouldBe(cycleId);
        body.Periods.Length.ShouldBe(3);
        // Ordered by PeriodNumber
        body.Periods[0].PeriodNumber.ShouldBe(1);
        body.Periods[1].PeriodNumber.ShouldBe(2);
        body.Periods[2].PeriodNumber.ShouldBe(3);
    }

    [Fact]
    public async Task GetCycleDetail_UnknownCycleId_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-cycle-detail2@example.com");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── REQ-READ-03: List CategoryGroups ─────────────────────────────────────

    [Fact]
    public async Task ListCategoryGroups_WithNestedCategories_ReturnsOrderedResult()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-groups1@example.com");
        var g1 = await CreateCategoryGroupAsync(budgetId, "Housing", 1);
        var g2 = await CreateCategoryGroupAsync(budgetId, "Transport", 2);
        await CreateCategoryAsync(budgetId, g1, "Rent", 1);
        await CreateCategoryAsync(budgetId, g1, "Utilities", 2);
        await CreateCategoryAsync(budgetId, g2, "Gas", 1);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/category-groups");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<CategoryGroupResponse[]>(JsonOpts);
        list!.Length.ShouldBe(2);
        list[0].Id.ShouldBe(g1);
        list[0].Categories.Length.ShouldBe(2);
        list[1].Id.ShouldBe(g2);
        list[1].Categories.Length.ShouldBe(1);
    }

    // ── REQ-READ-04: List BudgetLines ────────────────────────────────────────

    [Fact]
    public async Task ListBudgetLines_ShowsLatestRevisionAmount()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-lines1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId, amount: 1500m);

        // Update line to create a second revision with 2000 (revision split from today)
        await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                validFrom       = DateOnly.FromDateTime(DateTime.UtcNow),
                budgetedAmount  = 2000m,
                currencyId      = CurrencySeeds.GtqId,
            });

        // REQ-READ-04: list is budget-scoped, not period-scoped
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/lines");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lines = await response.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.Single().BudgetedAmount.ShouldBe(2000m);
    }

    [Fact]
    public async Task ListBudgetLines_ViewerCaller_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-lines2-owner@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId);

        var viewerToken = await SetupViewerAsync(budgetId, "read-lines2-viewer@example.com");
        AuthorizeClient(viewerToken);

        // REQ-READ-04: budget-scoped list endpoint
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/lines");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateBudgetLine_ViewerCaller_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("read-lines3-owner@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId);

        var viewerToken = await SetupViewerAsync(budgetId, "read-lines3-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/lines",
            new
            {
                name            = "Rent",
                lineType        = "Expense",
                categoryGroupId = groupId,
                startDate       = new DateOnly(2025, 1, 1),
                initialAmount   = 1500m,
                currencyId      = CurrencySeeds.GtqId,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record CycleListItem(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive, int PeriodCount);
    private sealed record CycleDetailResponse(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive, PeriodItem[] Periods);
    private sealed record PeriodItem(Guid Id, string Name, int PeriodNumber, DateOnly StartDate, DateOnly EndDate, bool IsClosed);
    private sealed record CategoryGroupResponse(Guid Id, string Name, int DisplayOrder, CategoryItem[] Categories);
    private sealed record CategoryItem(Guid Id, string Name, int DisplayOrder);
    private sealed record BudgetLineItem(Guid Id, string Name, string LineType, Guid CategoryGroupId, Guid? CategoryId, DateOnly StartDate, DateOnly? EndDate, decimal? BudgetedAmount, string? CurrencyCode, string? CurrencySymbol);
}
