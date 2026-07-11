using System.Net;
using System.Net.Http.Json;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for Cycle endpoints.
/// Covers REQ-CYC-01 to REQ-CYC-04.
/// </summary>
public sealed class CycleTests : BudgetStructureTestBase
{
    public CycleTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-CYC-01: Create Cycle ──────────────────────────────────────────────

    [Fact]
    public async Task CreateCycle_HappyPath_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-create1@example.com");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new { name = "Annual 2025", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 12, 31), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCycle_DateOverlap_Returns422WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-create2@example.com");
        await CreateCycleAsync(budgetId, "2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        // Overlapping range
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new { name = "Overlap", startDate = new DateOnly(2025, 6, 1), endDate = new DateOnly(2026, 6, 30), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("CYCLE_DATE_OVERLAP");
    }

    [Fact]
    public async Task CreateCycle_StartAfterEnd_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-create3@example.com");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new { name = "Bad dates", startDate = new DateOnly(2025, 12, 31), endDate = new DateOnly(2025, 1, 1), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateCycle_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/cycles",
            new { name = "X", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 12, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCycle_ViewerRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-create5-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "cycle-create5-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new { name = "X", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 12, 31), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── REQ-CYC-02: Update Cycle ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateCycle_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-update1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}",
            new { name = "Updated Name", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 12, 31), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateCycle_ShrinkRangeOrphansperiod_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-update2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, "2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        // Create a period in December
        await CreatePeriodAsync(budgetId, cycleId, "Dec", 12, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31));

        // Shrink to November — orphans December period
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}",
            new { name = "2025", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 11, 30), defaultCurrencyId = CurrencySeeds.GtqId });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("CYCLE_PERIOD_OUT_OF_RANGE");
    }

    // ── REQ-CYC-03: Delete Cycle ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteCycle_CascadeSoftDelete_Returns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-delete1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, "ToDelete");
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify GET returns empty list (cycle soft-deleted)
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<CycleListItem[]>(JsonOpts);
        list!.ShouldBeEmpty();
    }

    // ── REQ-CYC-04: Set Active Cycle ─────────────────────────────────────────

    [Fact]
    public async Task SetActiveCycle_AtomicSwap_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-active1@example.com");
        var cycleAId      = await CreateCycleAsync(budgetId, "A", new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));
        var cycleBId      = await CreateCycleAsync(budgetId, "B", new DateOnly(2025, 7, 1), new DateOnly(2025, 12, 31));

        // Activate A first
        await Client.PutAsJsonAsync($"/api/budgets/{budgetId}/active-cycle", new { cycleId = cycleAId });

        // Now swap to B
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/active-cycle",
            new { cycleId = cycleBId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify via ListCycles: B should be active, A should not
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        var list = await listResponse.Content.ReadFromJsonAsync<CycleListItem[]>(JsonOpts);
        list!.Single(c => c.Id == cycleBId).IsActive.ShouldBeTrue();
        list!.Single(c => c.Id == cycleAId).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task SetActiveCycle_NoPriorActive_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cycle-active2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/active-cycle",
            new { cycleId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
    private sealed record CycleListItem(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive, int PeriodCount);
}
