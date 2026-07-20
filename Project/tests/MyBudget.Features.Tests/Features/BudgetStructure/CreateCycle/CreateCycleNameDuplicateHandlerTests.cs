using MyBudget.Features.Features.BudgetStructure.CreateCycle;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCycle;

/// <summary>Tests for REQ-CYC-NAME-1: cycle name uniqueness per budget.</summary>
public sealed class CreateCycleNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateCycleHandler _sut;

    public CreateCycleNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateCycleHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private static CreateCycleCommand BuildCmd(Guid budgetId, string name) =>
        new(budgetId, name,
            StartDate:          new DateOnly(2026, 1, 1),
            EndDate:            new DateOnly(2026, 12, 31),
            DefaultCurrencyId:  CurrencySeeds.GtqId,
            AlternateCurrencyId: null,
            ExchangeRate:       null);

    [Fact]
    public async Task ActiveDuplicate_Returns_CYCLE_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        _db.Cycles.Add(Cycle.Create(budgetId, "Yearly 2025",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CurrencySeeds.GtqId));
        await _db.SaveChangesAsync();

        var cmd    = BuildCmd(budgetId, "Yearly 2025");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedDuplicate_Returns_CYCLE_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var deleted  = Cycle.Create(budgetId, "Yearly 2025",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CurrencySeeds.GtqId);
        deleted.SoftDelete();
        _db.Cycles.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = BuildCmd(budgetId, "Yearly 2025");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CYCLE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd    = BuildCmd(budgetId, "Yearly 2026");
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
    }
}
