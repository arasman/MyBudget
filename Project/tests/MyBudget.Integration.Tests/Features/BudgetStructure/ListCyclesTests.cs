using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for ListCycles with IncludeDeleted flag.
/// Covers soft-delete visibility and admin-only access guard.
/// </summary>
public sealed class ListCyclesTests : BudgetStructureTestBase
{
    public ListCyclesTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task ListCycles_IncludeDeletedFalse_ReturnsOnlyActiveCycles()
    {
        var (_, budgetId) = await SetupOwnerAsync("lc-active-only@example.com");
        await CreateCycleAsync(budgetId, "Active Cycle", new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));

        // Create and then soft-delete a second cycle via the delete endpoint
        var deletedCycleId = await CreateCycleAsync(budgetId, "Deleted Cycle", new DateOnly(2025, 7, 1), new DateOnly(2025, 12, 31));
        var deleteResp = await Client.DeleteAsync($"/api/budgets/{budgetId}/cycles/{deletedCycleId}");
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<CycleListItem[]>(JsonOpts);
        list!.Length.ShouldBe(1);
        list[0].DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListCycles_IncludeDeletedTrue_ReturnsBothCycles()
    {
        var (_, budgetId) = await SetupOwnerAsync("lc-include-deleted@example.com");
        var activeCycleId = await CreateCycleAsync(budgetId, "Active Cycle", new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));

        var deletedCycleId = await CreateCycleAsync(budgetId, "Deleted Cycle", new DateOnly(2025, 7, 1), new DateOnly(2025, 12, 31));
        var deleteResp = await Client.DeleteAsync($"/api/budgets/{budgetId}/cycles/{deletedCycleId}");
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles?includeDeleted=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<CycleListItem[]>(JsonOpts);
        list!.Length.ShouldBe(2);
        list.Single(c => c.Id == activeCycleId).DeletedAt.ShouldBeNull();
        list.Single(c => c.Id == deletedCycleId).DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListCycles_IncludeDeletedTrue_ViewerRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("lc-admin-guard-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "lc-admin-guard-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles?includeDeleted=true");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record CycleListItem(
        Guid              Id,
        string            Name,
        DateOnly          StartDate,
        DateOnly          EndDate,
        bool              IsActive,
        int               PeriodCount,
        DateTimeOffset?   DeletedAt = null);
}
