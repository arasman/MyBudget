using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.UpdateCycle;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCycle;

public sealed class UpdateCycleHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateCycleHandler _sut;

    public UpdateCycleHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateCycleHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid cycleId)> SeedCycleAsync()
    {
        // Currency seed rows are inserted by EF HasData when EnsureCreated runs
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(
            budgetId, "Original",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        return (budgetId, cycle.Id);
    }

    [Fact]
    public async Task Update_SetsCurrencyFields()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        var cmd = new UpdateCycleCommand(
            budgetId, cycleId,
            "Updated",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            CurrencySeeds.UsdId,
            CurrencySeeds.EurId,
            1.07m);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var cycle = await _db.Cycles.FindAsync(cycleId);
        cycle.ShouldNotBeNull();
        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.UsdId);
        cycle.AlternateCurrencyId.ShouldBe(CurrencySeeds.EurId);
        cycle.ExchangeRate.ShouldBe(1.07m);
    }

    [Fact]
    public async Task Update_ClearingAlternateCurrency_SetsNullFields()
    {
        var (budgetId, cycleId) = await SeedCycleAsync();

        // First set alternate
        var cycle = await _db.Cycles.FindAsync(cycleId);
        cycle!.Update("C", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            CurrencySeeds.GtqId, CurrencySeeds.UsdId, 7.5m);
        await _db.SaveChangesAsync();

        // Then clear it
        var cmd = new UpdateCycleCommand(
            budgetId, cycleId,
            "Updated",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            CurrencySeeds.GtqId,
            null,
            null);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _db.Entry(cycle).ReloadAsync();
        cycle.AlternateCurrencyId.ShouldBeNull();
        cycle.ExchangeRate.ShouldBeNull();
    }
}
