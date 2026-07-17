using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.RestorePeriod;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.RestorePeriod;

public sealed class RestorePeriodHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestorePeriodHandler _sut;

    public RestorePeriodHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new RestorePeriodHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid cycleId, Guid periodId)> SeedBaseAsync(bool cycleSoftDeleted = false)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);

        if (cycleSoftDeleted)
            cycle.SoftDelete();

        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        period.SoftDelete();
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        return (budgetId, cycle.Id, period.Id);
    }

    [Fact]
    public async Task HappyPath_RestoresPeriod()
    {
        var (budgetId, cycleId, periodId) = await SeedBaseAsync();

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, periodId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(periodId);

        var period = await _db.Periods.IgnoreQueryFilters().FirstAsync(p => p.Id == periodId);
        period.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task HappyPath_CascadeRestoresBudgetLines()
    {
        var (budgetId, cycleId, periodId) = await SeedBaseAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line1 = BudgetLine.Create(budgetId, periodId, group.Id, null, "Rent", LineType.Expense, true, 1);
        var line2 = BudgetLine.Create(budgetId, periodId, group.Id, null, "Food", LineType.Expense, false, 2);
        line1.SoftDelete();
        line2.SoftDelete();
        _db.BudgetLines.AddRange(line1, line2);
        await _db.SaveChangesAsync();

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, periodId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var lines = await _db.BudgetLines.IgnoreQueryFilters()
            .Where(bl => bl.PeriodId == periodId)
            .ToListAsync();
        lines.ShouldAllBe(bl => bl.DeletedAt == null);
    }

    [Fact]
    public async Task ParentCycleSoftDeleted_Returns_PARENT_IS_DELETED()
    {
        var (budgetId, cycleId, periodId) = await SeedBaseAsync(cycleSoftDeleted: true);

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, periodId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PARENT_IS_DELETED");
    }

    [Fact]
    public async Task PeriodNotFound_WhenNotSoftDeleted_Returns_PERIOD_NOT_FOUND()
    {
        var (budgetId, cycleId, periodId) = await SeedBaseAsync();

        // Restore first so it is no longer soft-deleted
        var period = await _db.Periods.IgnoreQueryFilters().FirstAsync(p => p.Id == periodId);
        period.Restore();
        await _db.SaveChangesAsync();

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, periodId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NOT_FOUND");
    }

    [Fact]
    public async Task PeriodNotFound_WhenWrongId_Returns_PERIOD_NOT_FOUND()
    {
        var (budgetId, cycleId, _) = await SeedBaseAsync();

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NOT_FOUND");
    }

    [Fact]
    public async Task CycleNotFound_Returns_CYCLE_NOT_FOUND()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd    = new RestorePeriodCommand(budgetId, Guid.NewGuid(), Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NOT_FOUND");
    }

    [Fact]
    public async Task IncludeExecutionRecords_RestoresPeriodLinessAndExecutionRecords()
    {
        var (budgetId, cycleId, periodId) = await SeedBaseAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, periodId, group.Id, null, "Rent", LineType.Expense, true, 1);
        line.SoftDelete();
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        var execRecord = ExecutionRecord.Create(
            budgetId, periodId, line.Id,
            EntryType.Expense, 500m, null,
            CurrencySeeds.GtqId, null, null, null, null);
        execRecord.SoftDelete();
        _db.ExecutionRecords.Add(execRecord);
        await _db.SaveChangesAsync();

        var cmd    = new RestorePeriodCommand(budgetId, cycleId, periodId, IncludeExecutionRecords: true);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var period = await _db.Periods.IgnoreQueryFilters().FirstAsync(p => p.Id == periodId);
        period.DeletedAt.ShouldBeNull();

        var restoredLine = await _db.BudgetLines.IgnoreQueryFilters().FirstAsync(bl => bl.Id == line.Id);
        restoredLine.DeletedAt.ShouldBeNull();

        var restoredRecord = await _db.ExecutionRecords.IgnoreQueryFilters().FirstAsync(e => e.Id == execRecord.Id);
        restoredRecord.DeletedAt.ShouldBeNull();
    }
}
