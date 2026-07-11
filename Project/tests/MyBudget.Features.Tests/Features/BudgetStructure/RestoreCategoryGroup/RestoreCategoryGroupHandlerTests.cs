using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.RestoreCategoryGroup;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.RestoreCategoryGroup;

public sealed class RestoreCategoryGroupHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestoreCategoryGroupHandler _sut;

    public RestoreCategoryGroupHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new RestoreCategoryGroupHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid cycleId, Guid periodId, CategoryGroup group)> SeedGroupAsync()
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
        group.SoftDelete();
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, cycle.Id, period.Id, group);
    }

    [Fact]
    public async Task FullCascade_RestoresGroup_Categories_And_BudgetLines()
    {
        var (budgetId, _, periodId, group) = await SeedGroupAsync();

        var cat1 = Category.Create(group.Id, "Cat1", 1);
        cat1.SoftDelete();
        var cat2 = Category.Create(group.Id, "Cat2", 2);
        cat2.SoftDelete();
        _db.Categories.AddRange(cat1, cat2);
        await _db.SaveChangesAsync();

        // BudgetLines scoped by CategoryGroupId
        var line1 = BudgetLine.Create(periodId, group.Id, cat1.Id, "Rent",      LineType.Expense, true, 1);
        var line2 = BudgetLine.Create(periodId, group.Id, cat1.Id, "Utilities", LineType.Expense, false, 2);
        var line3 = BudgetLine.Create(periodId, group.Id, cat2.Id, "Insurance", LineType.Expense, false, 1);
        var line4 = BudgetLine.Create(periodId, group.Id, cat2.Id, "Food",      LineType.Expense, false, 2);
        line1.SoftDelete(); line2.SoftDelete(); line3.SoftDelete(); line4.SoftDelete();
        _db.BudgetLines.AddRange(line1, line2, line3, line4);
        await _db.SaveChangesAsync();

        var cmd    = new RestoreCategoryGroupCommand(budgetId, group.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var restoredGroup = await _db.CategoryGroups.IgnoreQueryFilters().FirstAsync(g => g.Id == group.Id);
        restoredGroup.DeletedAt.ShouldBeNull();

        var categories = await _db.Categories.IgnoreQueryFilters()
            .Where(c => c.CategoryGroupId == group.Id)
            .ToListAsync();
        categories.ShouldAllBe(c => c.DeletedAt == null);

        var lines = await _db.BudgetLines.IgnoreQueryFilters()
            .Where(bl => bl.CategoryGroupId == group.Id)
            .ToListAsync();
        lines.ShouldAllBe(bl => bl.DeletedAt == null);
    }

    [Fact]
    public async Task GroupNotFound_Returns_CATEGORY_GROUP_NOT_FOUND()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd    = new RestoreCategoryGroupCommand(budgetId, Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NOT_FOUND");
    }
}
