using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.CreateCycle;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCycle;

public sealed class CreateCycleHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateCycleHandler _sut;

    public CreateCycleHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateCycleHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_WithCurrencyFields_PersistsThem()
    {
        // Currency seed rows are inserted by EF HasData when EnsureCreated runs
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd = new CreateCycleCommand(
            budgetId,
            "2025",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31),
            CurrencySeeds.GtqId,
            CurrencySeeds.UsdId,
            7.5m);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var cycle = await _db.Cycles.FindAsync(result.Value);
        cycle.ShouldNotBeNull();
        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.GtqId);
        cycle.AlternateCurrencyId.ShouldBe(CurrencySeeds.UsdId);
        cycle.ExchangeRate.ShouldBe(7.5m);
    }

    [Fact]
    public async Task Create_WithoutAlternateCurrency_PersistsNullFields()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd = new CreateCycleCommand(
            budgetId,
            "2025",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31),
            CurrencySeeds.GtqId,
            null,
            null);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var cycle = await _db.Cycles.FindAsync(result.Value);
        cycle.ShouldNotBeNull();
        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.GtqId);
        cycle.AlternateCurrencyId.ShouldBeNull();
        cycle.ExchangeRate.ShouldBeNull();
    }
}
