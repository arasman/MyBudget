using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for RestoreExecutionRecord endpoint.
/// Covers REQ-EXEC-RESTORE-1, REQ-EXEC-RESTORE-2, REQ-EXEC-CLOSED-1, REQ-EXEC-RESTORE-DATERANGE-1.
/// </summary>
public sealed class RestoreExecutionRecordIntegrationTests : BudgetExecutionTestBase
{
    public RestoreExecutionRecordIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    /// <summary>
    /// Inserts a soft-deleted ExecutionRecord directly via EF, bypassing handler guards.
    /// Used to set up scenarios where the Period is outside the BudgetLine date range
    /// (which the CreateExecutionRecord handler would reject).
    /// </summary>
    private async Task<Guid> SeedSoftDeletedRecordAsync(
        Guid      budgetId,
        Guid      periodId,
        Guid      lineId,
        DateOnly? operationDate = null)
    {
        using var scope  = Factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record       = ExecutionRecord.Create(
            budgetId, periodId, lineId,
            EntryType.Expense, 100m, null,
            GtqId, null, null, null, null,
            operationDate ?? new DateOnly(2025, 1, 15));
        record.SoftDelete();
        db.ExecutionRecords.Add(record);
        await db.SaveChangesAsync();
        return record.Id;
    }

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

    // ── REQ-EXEC-RESTORE-DATERANGE-1: BudgetLine date-range guard ────────────

    [Fact]
    public async Task Restore_PeriodStartsBeforeBudgetLineStart_Returns422WithCode()
    {
        // BudgetLine starts 2025-02-01; Period is Jan 2025 → period starts before line
        var (_, budgetId) = await SetupOwnerAsync("exec-restore-dr1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId,
            startDate: new DateOnly(2025, 2, 1));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId);

        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await restoreResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("EXECUTION_OUT_OF_DATE_RANGE");
    }

    [Fact]
    public async Task Restore_PeriodEndsAfterBudgetLineEnd_Returns422WithCode()
    {
        // BudgetLine ends 2024-12-31; Period is Jan 2025 → period ends after line
        var (_, budgetId) = await SetupOwnerAsync("exec-restore-dr2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId,
            startDate: new DateOnly(2020, 1, 1),
            endDate:   new DateOnly(2024, 12, 31));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId);

        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await restoreResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("EXECUTION_OUT_OF_DATE_RANGE");
    }

    [Fact]
    public async Task Restore_OperationDateOutsideRangeButPeriodInside_Returns200()
    {
        // Period (Jan 2025) is within BudgetLine (2020–2025-12-31); OperationDate is 2019 — irrelevant
        var (_, budgetId) = await SetupOwnerAsync("exec-restore-dr3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId,
            start: new DateOnly(2025, 1, 1), end: new DateOnly(2025, 1, 31));
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, Guid.Empty, groupId,
            startDate: new DateOnly(2020, 1, 1),
            endDate:   new DateOnly(2025, 12, 31));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId,
            operationDate: new DateOnly(2019, 6, 15));

        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{execId}/restore",
            null);

        restoreResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
