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

    private async Task<(Guid budgetId, Guid groupId)> SeedBaseAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, group.Id);
    }

    [Fact]
    public async Task ValidReorder_AssignsSequentialDisplayOrder()
    {
        var (budgetId, groupId) = await SeedBaseAsync();

        var line1 = BudgetLine.Create(budgetId, groupId, null, "Rent",      LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 1);
        var line2 = BudgetLine.Create(budgetId, groupId, null, "Utilities", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 2);
        var line3 = BudgetLine.Create(budgetId, groupId, null, "Insurance", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 3);
        _db.BudgetLines.AddRange(line1, line2, line3);
        await _db.SaveChangesAsync();

        // Reverse the order: line3=1, line2=2, line1=3
        var cmd    = new ReorderBudgetLinesCommand(budgetId, [line3.Id, line2.Id, line1.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        using var verifyDb = DbTestHelpers.CreateSqliteContext();
        // Re-fetch from same in-memory DB context — just verify via the tracked entities
        var reloaded1 = await _db.BudgetLines.FindAsync(line1.Id);
        var reloaded3 = await _db.BudgetLines.FindAsync(line3.Id);
        reloaded3!.DisplayOrder.ShouldBe(1);
        reloaded1!.DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public async Task ForeignId_Returns_REORDER_ID_NOT_IN_SCOPE()
    {
        var (budgetId, groupId) = await SeedBaseAsync();

        var line = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense, DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 1);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        var foreignId = Guid.NewGuid();
        var cmd       = new ReorderBudgetLinesCommand(budgetId, [line.Id, foreignId]);
        var result    = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_ID_NOT_IN_SCOPE");
    }

    [Fact]
    public async Task DuplicateId_IsRejectedByValidator()
    {
        var validator = new ReorderBudgetLinesValidator();
        var lineId    = Guid.NewGuid();

        var cmd    = new ReorderBudgetLinesCommand(Guid.NewGuid(), [lineId, lineId]);
        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "REORDER_DUPLICATE_ID");
    }
}
