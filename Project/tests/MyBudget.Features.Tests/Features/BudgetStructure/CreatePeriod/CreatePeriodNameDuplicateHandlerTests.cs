using MyBudget.Features.Features.BudgetStructure.CreatePeriod;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreatePeriod;

/// <summary>Tests for REQ-PER-NAME-1: period name uniqueness per cycle.</summary>
public sealed class CreatePeriodNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreatePeriodHandler _sut;

    public CreatePeriodNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreatePeriodHandler(_db);
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
    public async Task SoftDeletedDuplicate_Returns_PERIOD_NAME_DUPLICATE()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        var deleted = Period.Create(budgetId, cycleId, "January", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        deleted.SoftDelete();
        _db.Periods.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = new CreatePeriodCommand(budgetId, cycleId, "January", 2,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NAME_DUPLICATE");
    }

    [Fact]
    public async Task ActiveDuplicate_Returns_PERIOD_NAME_DUPLICATE()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        _db.Periods.Add(Period.Create(budgetId, cycleId, "January", 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        await _db.SaveChangesAsync();

        var cmd    = new CreatePeriodCommand(budgetId, cycleId, "January", 2,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        var cmd    = new CreatePeriodCommand(budgetId, cycleId, "February", 1,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
