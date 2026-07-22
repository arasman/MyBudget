using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for BudgetLine soft-delete cascade to ExecutionRecords.
/// Covers REQ-EXEC-CASCADE-1.
/// </summary>
public sealed class DeleteBudgetLineCascadeIntegrationTests : BudgetExecutionTestBase
{
    public DeleteBudgetLineCascadeIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-EXEC-CASCADE-1: Delete BudgetLine cascades to ExecutionRecords ───

    [Fact]
    public async Task DeleteBudgetLine_CascadesSoftDeleteToExecutionRecords()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-cascade-delete1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        // Seed 2 ExecutionRecords
        var id1 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);
        var id2 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 200m);

        // Soft-delete the BudgetLine (route is now budget-scoped, no periodId)
        var deleteResp = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/lines/{lineId}");
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert both ExecutionRecords are now soft-deleted
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exec1 = await db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id1);

        var exec2 = await db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id2);

        exec1.ShouldNotBeNull();
        exec1!.DeletedAt.ShouldNotBeNull("ExecutionRecord 1 should be soft-deleted");

        exec2.ShouldNotBeNull();
        exec2!.DeletedAt.ShouldNotBeNull("ExecutionRecord 2 should be soft-deleted");
    }
}
