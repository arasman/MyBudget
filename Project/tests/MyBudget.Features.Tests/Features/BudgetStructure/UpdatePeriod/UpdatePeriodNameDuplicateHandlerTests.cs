using MyBudget.Features.Features.BudgetStructure.UpdatePeriod;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdatePeriod;

/// <summary>Tests for REQ-PER-NAME-1 in update path: period name uniqueness per cycle.</summary>
public sealed class UpdatePeriodNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdatePeriodHandler _sut;

    public UpdatePeriodNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdatePeriodHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid cycleId)> SeedCycleAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var cycle    = Cycle.Create(budgetId, "Test Cycle",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();
        return (budgetId, cycle.Id);
    }

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_PERIOD_NAME_DUPLICATE()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        var deleted = Period.Create(budgetId, cycleId, "February", 2,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        deleted.SoftDelete();
        _db.Periods.Add(deleted);

        var target = Period.Create(budgetId, cycleId, "January", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        _db.Periods.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdatePeriodCommand(budgetId, cycleId, target.Id, "February", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        var target = Period.Create(budgetId, cycleId, "January", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        _db.Periods.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdatePeriodCommand(budgetId, cycleId, target.Id, "January", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
