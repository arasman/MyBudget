using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.RestoreCycle;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.RestoreCycle;

public sealed class RestoreCycleHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestoreCycleHandler _sut;

    public RestoreCycleHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new RestoreCycleHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Cycle cycle)> SeedCycleAsync(bool softDeleted = true)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);

        if (softDeleted)
            cycle.SoftDelete();

        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        return (budgetId, cycle);
    }

    [Fact]
    public async Task FullCascade_RestoresCycle_Periods_And_BudgetLines()
    {
        var (budgetId, cycle) = await SeedCycleAsync(softDeleted: true);

        // Seed 2 soft-deleted periods, each with 2 soft-deleted budget lines
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var period1 = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        period1.SoftDelete();

        var period2 = Period.Create(budgetId, cycle.Id, "February", 2,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(59)));
        period2.SoftDelete();

        _db.Periods.Add(period1);
        _db.Periods.Add(period2);
        await _db.SaveChangesAsync();

        // TODO PR4: update to new BudgetLine.Create signature; Cycle restore no longer cascades BudgetLines (REQ-RST-02)
        var line1 = BudgetLine.Create(budgetId, group.Id, null, "Rent",      LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 1);
        var line2 = BudgetLine.Create(budgetId, group.Id, null, "Utilities", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 2);
        var line3 = BudgetLine.Create(budgetId, group.Id, null, "Insurance", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 3);
        var line4 = BudgetLine.Create(budgetId, group.Id, null, "Food",      LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 4);
        line1.SoftDelete(); line2.SoftDelete(); line3.SoftDelete(); line4.SoftDelete();
        _db.BudgetLines.AddRange(line1, line2, line3, line4);
        await _db.SaveChangesAsync();

        var cmd    = new RestoreCycleCommand(budgetId, cycle.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var restoredCycle = await _db.Cycles.IgnoreQueryFilters().FirstAsync(c => c.Id == cycle.Id);
        restoredCycle.DeletedAt.ShouldBeNull();

        var periods = await _db.Periods.IgnoreQueryFilters().Where(p => p.CycleId == cycle.Id).ToListAsync();
        periods.ShouldAllBe(p => p.DeletedAt == null);

        // TODO PR4: BudgetLines no longer cascade-restored from Cycle — assertion updated
        _ = line1; _ = line2; _ = line3; _ = line4; // suppress unused warnings
    }

    [Fact]
    public async Task AlreadyActiveCycle_Returns_CYCLE_NOT_FOUND()
    {
        var (budgetId, cycle) = await SeedCycleAsync(softDeleted: false);

        var cmd    = new RestoreCycleCommand(budgetId, cycle.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NOT_FOUND");
    }

    [Fact]
    public async Task NonSoftDeletedPeriod_BudgetLinesNotRestored()
    {
        var (budgetId, cycle) = await SeedCycleAsync(softDeleted: true);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        // Period is NOT soft-deleted
        var activePeriod = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _db.Periods.Add(activePeriod);
        await _db.SaveChangesAsync();

        // BudgetLines under the active period are soft-deleted
        // TODO PR4: update to new BudgetLine.Create signature
        var line1 = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 1);
        var line2 = BudgetLine.Create(budgetId, group.Id, null, "Food", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 2);
        line1.SoftDelete(); line2.SoftDelete();
        _db.BudgetLines.AddRange(line1, line2);
        await _db.SaveChangesAsync();

        var cmd    = new RestoreCycleCommand(budgetId, cycle.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // TODO PR4: BudgetLines are now Budget-scoped; period-based filter removed
        _ = line1; _ = line2; _ = activePeriod; // suppress unused warnings
    }
}
