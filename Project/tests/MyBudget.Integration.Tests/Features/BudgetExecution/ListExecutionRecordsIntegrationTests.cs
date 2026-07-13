using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Integration tests for ListExecutionRecords endpoint.
/// Covers REQ-EXEC-LIST-1, REQ-EXEC-LIST-2.
/// </summary>
public sealed class ListExecutionRecordsIntegrationTests : BudgetExecutionTestBase
{
    public ListExecutionRecordsIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record ExecutionRecordDto(
        Guid     Id,
        int      EntryType,
        decimal  Amount,
        Guid     CurrencyId,
        decimal? ExchangeRate,
        decimal? ExchangeRateTo,
        Guid?    AccountId,
        Guid?    PaymentMethodId,
        string?  Note,
        DateTimeOffset  CreatedAt,
        DateTimeOffset? UpdatedAt);

    // ── REQ-EXEC-LIST-1: Returns non-deleted records ordered ASC ─────────────

    [Fact]
    public async Task ListExecutionRecords_ReturnsNonDeletedOrderedByCreatedAt()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-list1@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        // Create 3 records
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 100m);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 200m);
        var id3 = await CreateExecutionRecordAsync(budgetId, periodId, lineId, 300m);

        // Soft-delete the 3rd one
        await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{id3}");

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ExecutionRecordDto>>(JsonOpts);
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(2);

        // REQ-EXEC-LIST-1: ordered by CreatedAt ASC
        items[0].Amount.ShouldBe(100m);
        items[1].Amount.ShouldBe(200m);
    }

    // ── REQ-EXEC-LIST-2: Response shape ──────────────────────────────────────

    [Fact]
    public async Task ListExecutionRecords_ResponseIncludesRequiredFields()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-list2@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        await CreateExecutionRecordAsync(budgetId, periodId, lineId, 150m, note: "test note", entryType: 1);

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ExecutionRecordDto>>(JsonOpts);
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(1);

        var item = items[0];
        item.Id.ShouldNotBe(Guid.Empty);
        item.EntryType.ShouldBe(1);
        item.Amount.ShouldBe(150m);
        item.CurrencyId.ShouldBe(GtqId);
        item.Note.ShouldBe("test note");
        item.CreatedAt.ShouldNotBe(default);
        // UpdatedAt is null for newly-created records (set only on update)
        item.UpdatedAt.HasValue.ShouldBeFalse();
    }

    // ── Empty list ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListExecutionRecords_EmptyLine_ReturnsEmptyArray()
    {
        var (_, budgetId) = await SetupOwnerAsync("exec-list3@example.com");
        var cycleId       = await CreateCycleAsync(budgetId);
        var periodId      = await CreatePeriodAsync(budgetId, cycleId);
        var groupId       = await CreateCategoryGroupAsync(budgetId);
        var lineId        = await CreateBudgetLineAsync(budgetId, periodId, groupId);

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ExecutionRecordDto>>(JsonOpts);
        items.ShouldNotBeNull();
        items!.ShouldBeEmpty();
    }
}
