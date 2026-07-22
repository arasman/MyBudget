using MyBudget.Features.Features.BudgetExecution.RestoreExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.RestoreExecutionRecord;

/// <summary>
/// Unit tests for RestoreExecutionRecordHandler date-range guard.
/// REQ-EXEC-RESTORE-DATERANGE-1: Period [StartDate, EndDate] must fall within BudgetLine [StartDate, EndDate].
/// OperationDate is irrelevant to this check — only Period dates apply.
/// </summary>
public sealed class RestoreExecutionRecordDateRangeHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestoreExecutionRecordHandler _sut;

    public RestoreExecutionRecordDateRangeHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new RestoreExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, Guid lineId)> SeedAsync(
        DateOnly  lineStart,
        DateOnly? lineEnd,
        DateOnly  periodStart,
        DateOnly  periodEnd)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Cycle",
            new DateOnly(2020, 1, 1), new DateOnly(2030, 12, 31),
            CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "Test Period", 1, periodStart, periodEnd);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            lineStart, lineEnd, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, line.Id);
    }

    /// <summary>Inserts a soft-deleted ExecutionRecord directly via EF, bypassing handler guards.</summary>
    private async Task<Guid> SeedSoftDeletedRecordAsync(
        Guid      budgetId,
        Guid      periodId,
        Guid      lineId,
        DateOnly? operationDate = null)
    {
        var record = ExecutionRecord.Create(
            budgetId, periodId, lineId,
            EntryType.Expense, 100m, null,
            CurrencySeeds.GtqId, null, null, null, null,
            operationDate);
        record.SoftDelete();
        _db.ExecutionRecords.Add(record);
        await _db.SaveChangesAsync();
        return record.Id;
    }

    // ── (a) Period within BudgetLine range — passes ───────────────────────────

    [Fact]
    public async Task Restore_PeriodWithinBudgetLineRange_Succeeds()
    {
        var (budgetId, periodId, lineId) = await SeedAsync(
            lineStart:   new DateOnly(2025, 1, 1),
            lineEnd:     null,
            periodStart: new DateOnly(2025, 3, 1),
            periodEnd:   new DateOnly(2025, 3, 31));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId);

        var result = await _sut.Handle(
            new RestoreExecutionRecordCommand(budgetId, periodId, lineId, execId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    // ── (b) Period starts before BudgetLine start — rejected ─────────────────

    [Fact]
    public async Task Restore_PeriodStartsBeforeBudgetLineStart_ReturnsExecutionOutOfDateRange()
    {
        var (budgetId, periodId, lineId) = await SeedAsync(
            lineStart:   new DateOnly(2025, 2, 1),
            lineEnd:     null,
            periodStart: new DateOnly(2025, 1, 1),
            periodEnd:   new DateOnly(2025, 1, 31));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId);

        var result = await _sut.Handle(
            new RestoreExecutionRecordCommand(budgetId, periodId, lineId, execId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXECUTION_OUT_OF_DATE_RANGE");
    }

    // ── (c) Period ends after BudgetLine end — rejected ──────────────────────

    [Fact]
    public async Task Restore_PeriodEndsAfterBudgetLineEnd_ReturnsExecutionOutOfDateRange()
    {
        var (budgetId, periodId, lineId) = await SeedAsync(
            lineStart:   new DateOnly(2020, 1, 1),
            lineEnd:     new DateOnly(2024, 12, 31),
            periodStart: new DateOnly(2025, 1, 1),
            periodEnd:   new DateOnly(2025, 1, 31));

        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId);

        var result = await _sut.Handle(
            new RestoreExecutionRecordCommand(budgetId, periodId, lineId, execId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXECUTION_OUT_OF_DATE_RANGE");
    }

    // ── (d) OperationDate outside range but period inside — passes ────────────

    [Fact]
    public async Task Restore_OperationDateOutsideRangeButPeriodInside_Succeeds()
    {
        var (budgetId, periodId, lineId) = await SeedAsync(
            lineStart:   new DateOnly(2025, 1, 1),
            lineEnd:     new DateOnly(2025, 12, 31),
            periodStart: new DateOnly(2025, 3, 1),
            periodEnd:   new DateOnly(2025, 3, 31));

        // OperationDate is 2019 — far outside BudgetLine range — must NOT block restore
        var execId = await SeedSoftDeletedRecordAsync(budgetId, periodId, lineId,
            operationDate: new DateOnly(2019, 6, 15));

        var result = await _sut.Handle(
            new RestoreExecutionRecordCommand(budgetId, periodId, lineId, execId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
