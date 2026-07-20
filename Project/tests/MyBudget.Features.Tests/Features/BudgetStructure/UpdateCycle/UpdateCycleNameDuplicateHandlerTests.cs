using MyBudget.Features.Features.BudgetStructure.UpdateCycle;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCycle;

/// <summary>Tests for REQ-CYC-NAME-1 in update path: cycle name uniqueness per budget.</summary>
public sealed class UpdateCycleNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateCycleHandler _sut;

    public UpdateCycleNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateCycleHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private static UpdateCycleCommand BuildCmd(Guid budgetId, Guid cycleId, string name) =>
        new(budgetId, cycleId, name,
            StartDate:          new DateOnly(2026, 1, 1),
            EndDate:            new DateOnly(2026, 12, 31),
            DefaultCurrencyId:  CurrencySeeds.GtqId,
            AlternateCurrencyId: null,
            ExchangeRate:       null);

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_CYCLE_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var deleted = Cycle.Create(budgetId, "Cycle B",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CurrencySeeds.GtqId);
        deleted.SoftDelete();
        _db.Cycles.Add(deleted);

        var target = Cycle.Create(budgetId, "Cycle A",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = BuildCmd(budgetId, target.Id, "Cycle B");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var target = Cycle.Create(budgetId, "Cycle A",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = BuildCmd(budgetId, target.Id, "Cycle A");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveSiblingDuplicate_Returns_CYCLE_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        _db.Cycles.Add(Cycle.Create(budgetId, "Cycle B",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CurrencySeeds.GtqId));
        var target = Cycle.Create(budgetId, "Cycle A",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = BuildCmd(budgetId, target.Id, "Cycle B");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NAME_DUPLICATE");
    }
}
