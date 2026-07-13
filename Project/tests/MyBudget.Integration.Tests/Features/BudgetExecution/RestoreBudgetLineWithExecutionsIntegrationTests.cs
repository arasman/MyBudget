using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for RestoreBudgetLine with IncludeExecutionRecords flag.
/// Covers REQ-EXEC-CASCADE-2.
/// </summary>
public sealed class RestoreBudgetLineWithExecutionsIntegrationTests : BudgetExecutionTestBase
{
    public RestoreBudgetLineWithExecutionsIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-EXEC-CASCADE-2: includeExecutionRecords=true restores children ───

    [Fact]
    public async Task RestoreBudgetLine_IncludeExecutionRecordsTrue_RestoresChildren()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-restore-line1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var id1 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);
        var id2 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 200m);

        // Soft-delete the line (cascades to execution records)
        await Client.DeleteAsync($"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}");

        // Restore BudgetLine with includeExecutionRecords=true
        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}/restore?includeExecutionRecords=true",
            null);
        restoreResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert both ExecutionRecords are restored
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exec1 = await db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id1);
        var exec2 = await db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id2);

        exec1.ShouldNotBeNull();
        exec1!.DeletedAt.ShouldBeNull("ExecutionRecord 1 should be restored");

        exec2.ShouldNotBeNull();
        exec2!.DeletedAt.ShouldBeNull("ExecutionRecord 2 should be restored");
    }

    // ── REQ-EXEC-CASCADE-2: includeExecutionRecords=false leaves children deleted ──

    [Fact]
    public async Task RestoreBudgetLine_IncludeExecutionRecordsFalse_LeavesChildrenDeleted()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-restore-line2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var id1 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);

        // Soft-delete the line (cascades to execution records)
        await Client.DeleteAsync($"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}");

        // Restore BudgetLine WITHOUT includeExecutionRecords
        var restoreResp = await Client.PostAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}/restore?includeExecutionRecords=false",
            null);
        restoreResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert ExecutionRecord remains soft-deleted
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exec1 = await db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id1);

        exec1.ShouldNotBeNull();
        exec1!.DeletedAt.ShouldNotBeNull("ExecutionRecord should remain soft-deleted");
    }
}
