using MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.ReorderBudgetLines;

public sealed class ReorderBudgetLinesHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReorderBudgetLinesHandler _sut;

    public ReorderBudgetLinesHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new ReorderBudgetLinesHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, Guid groupId)> SeedPeriodAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, group.Id);
    }

    [Fact]
    public async Task ValidReorder_AssignsSequentialDisplayOrder()
    {
        var (budgetId, periodId, groupId) = await SeedPeriodAsync();

        var line1 = BudgetLine.Create(periodId, groupId, null, "Rent",      LineType.Expense, true,  1);
        var line2 = BudgetLine.Create(periodId, groupId, null, "Utilities", LineType.Expense, false, 2);
        var line3 = BudgetLine.Create(periodId, groupId, null, "Insurance", LineType.Expense, false, 3);
        _db.BudgetLines.AddRange(line1, line2, line3);
        await _db.SaveChangesAsync();

        // Reverse order: line3 = 1, line2 = 2, line1 = 3
        var cmd    = new ReorderBudgetLinesCommand(budgetId, periodId, [line3.Id, line2.Id, line1.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _db.Entry(line1).ReloadAsync();
        await _db.Entry(line2).ReloadAsync();
        await _db.Entry(line3).ReloadAsync();

        line3.DisplayOrder.ShouldBe(1);
        line2.DisplayOrder.ShouldBe(2);
        line1.DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public async Task ForeignId_Returns_REORDER_ID_NOT_IN_SCOPE()
    {
        var (budgetId, periodId, groupId) = await SeedPeriodAsync();

        var line = BudgetLine.Create(periodId, groupId, null, "Rent", LineType.Expense, true, 1);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        // Include an ID that does not belong to this Period
        var foreignId = Guid.NewGuid();
        var cmd       = new ReorderBudgetLinesCommand(budgetId, periodId, [line.Id, foreignId]);
        var result    = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_ID_NOT_IN_SCOPE");
    }

    [Fact]
    public async Task DuplicateId_IsRejectedByValidator()
    {
        var validator = new ReorderBudgetLinesValidator();
        var lineId    = Guid.NewGuid();

        var cmd    = new ReorderBudgetLinesCommand(Guid.NewGuid(), Guid.NewGuid(), [lineId, lineId]);
        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "REORDER_DUPLICATE_ID");
    }

    [Fact]
    public async Task PeriodNotFound_Returns_PERIOD_NOT_FOUND()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd    = new ReorderBudgetLinesCommand(budgetId, Guid.NewGuid(), [Guid.NewGuid()]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NOT_FOUND");
    }
}
