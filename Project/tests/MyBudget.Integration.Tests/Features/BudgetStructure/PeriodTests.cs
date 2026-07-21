using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for Period endpoints.
/// Covers REQ-PER-01 to REQ-PER-04.
/// </summary>
public sealed class PeriodTests : BudgetStructureTestBase
{
    public PeriodTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-PER-01: Create Period ─────────────────────────────────────────────

    [Fact]
    public async Task CreatePeriod_HappyPath_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-create1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods",
            new { name = "January", periodNumber = 1, startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 1, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreatePeriod_DatesOutsideCycleRange_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-create2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId, "2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods",
            new { name = "Bad", periodNumber = 1, startDate = new DateOnly(2025, 12, 1), endDate = new DateOnly(2026, 1, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_OUT_OF_CYCLE_RANGE");
    }

    [Fact]
    public async Task CreatePeriod_DateOverlap_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-create3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        // First period — January
        await CreatePeriodAsync(budgetId, cycleId, "Jan", 1, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));

        // Overlapping period
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods",
            new { name = "Overlap", periodNumber = 2, startDate = new DateOnly(2025, 1, 15), endDate = new DateOnly(2025, 2, 15) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_DATE_OVERLAP");
    }

    [Fact]
    public async Task CreatePeriod_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/cycles/{Guid.NewGuid()}/periods",
            new { name = "X", periodNumber = 1, startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 1, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePeriod_ViewerRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-create5-owner@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var viewerToken   = await SetupViewerAsync(budgetId, "period-create5-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods",
            new { name = "X", periodNumber = 1, startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 1, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── REQ-PER-02: Update Period ─────────────────────────────────────────────

    [Fact]
    public async Task UpdatePeriod_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-update1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}",
            new { name = "January Updated", periodNumber = 1, startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 1, 28) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── REQ-PER-03: Set Period Status ─────────────────────────────────────────

    [Fact]
    public async Task SetPeriodStatus_Close_Returns200_IsClosed()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-status1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);

        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetPeriodStatus_Reopen_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("period-status2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);

        // Close it first
        await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });

        // Reopen
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── REQ-PER-04: Delete Period ─────────────────────────────────────────────

    [Fact]
    public async Task DeletePeriod_Returns204_BudgetLinesUnaffected()
    {
        // REQ-CYC-03: BudgetLines MUST NOT be cascade-deleted when Period is deleted.
        // BudgetLines are Budget-scoped, not Period-scoped.
        var (_, budgetId) = await SetupOwnerAsync("period-delete1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // BudgetLine is still present at the budget-scoped list endpoint
        var linesResponse = await Client.GetAsync($"/api/budgets/{budgetId}/lines");
        linesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lines = await linesResponse.Content.ReadFromJsonAsync<BudgetLineItem[]>(JsonOpts);
        lines!.Length.ShouldBe(1);
    }

    private sealed record BudgetLineItem(Guid Id, string Name);

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
}
