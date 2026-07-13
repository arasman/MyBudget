using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for RestoreExecutionRecord endpoint.
/// Covers REQ-EXEC-RESTORE-1, REQ-EXEC-RESTORE-2, REQ-EXEC-CLOSED-1.
/// </summary>
public sealed class RestoreExecutionRecordIntegrationTests : BudgetExecutionTestBase
{
    public RestoreExecutionRecordIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record ErrorResponse(string Error);

    // ── REQ-EXEC-RESTORE-1: Happy path restore ───────────────────────────────

    [Fact]
    public async Task Restore_SoftDeletedRecord_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-restore1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var execId = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);

        // Soft-delete it first
        var deleteResp = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}");
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Now restore
        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── REQ-EXEC-RESTORE-2: Non-deleted record → 404 ─────────────────────────

    [Fact]
    public async Task Restore_ActiveRecord_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-restore2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var execId = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);

        // Try to restore without deleting first
        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── REQ-EXEC-CLOSED-1: Period closed guard on restore ────────────────────

    [Fact]
    public async Task Restore_ClosedPeriod_Returns409WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-restore3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var execId = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);

        // Delete the record while period is open
        await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}");

        // Close the period
        await ClosePeriodAsync(budgetId, cycleId, periodId);

        // Attempt restore on closed period
        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await restoreResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("PERIOD_CLOSED");
    }
}
