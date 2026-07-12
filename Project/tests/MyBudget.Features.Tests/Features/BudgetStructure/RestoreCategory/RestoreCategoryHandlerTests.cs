using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.RestoreCategory;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.RestoreCategory;

public sealed class RestoreCategoryHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestoreCategoryHandler _sut;

    public RestoreCategoryHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new RestoreCategoryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, CategoryGroup group)> SeedBaseAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, group);
    }

    [Fact]
    public async Task Cascade_RestoresCategory_And_BudgetLines()
    {
        var (budgetId, periodId, group) = await SeedBaseAsync();

        var category = Category.Create(budgetId, group.Id, "Rent", 1);
        category.SoftDelete();
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var line1 = BudgetLine.Create(budgetId, periodId, group.Id, category.Id, "Rent 1", LineType.Expense, true, 1);
        var line2 = BudgetLine.Create(budgetId, periodId, group.Id, category.Id, "Rent 2", LineType.Expense, false, 2);
        var line3 = BudgetLine.Create(budgetId, periodId, group.Id, category.Id, "Rent 3", LineType.Expense, false, 3);
        line1.SoftDelete(); line2.SoftDelete(); line3.SoftDelete();
        _db.BudgetLines.AddRange(line1, line2, line3);
        await _db.SaveChangesAsync();

        var cmd    = new RestoreCategoryCommand(budgetId, group.Id, category.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var restoredCategory = await _db.Categories.IgnoreQueryFilters().FirstAsync(c => c.Id == category.Id);
        restoredCategory.DeletedAt.ShouldBeNull();

        var lines = await _db.BudgetLines.IgnoreQueryFilters()
            .Where(bl => bl.CategoryId == category.Id)
            .ToListAsync();
        lines.Count.ShouldBe(3);
        lines.ShouldAllBe(bl => bl.DeletedAt == null);
    }

    [Fact]
    public async Task ParentGroupSoftDeleted_Returns_PARENT_IS_DELETED()
    {
        var (budgetId, _, group) = await SeedBaseAsync();

        // Soft-delete the parent group
        group.SoftDelete();
        await _db.SaveChangesAsync();

        var category = Category.Create(budgetId, group.Id, "Rent", 1);
        category.SoftDelete();
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var cmd    = new RestoreCategoryCommand(budgetId, group.Id, category.Id, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PARENT_IS_DELETED");
    }

    [Fact]
    public async Task CategoryNotFound_Returns_CATEGORY_NOT_FOUND()
    {
        var (budgetId, _, group) = await SeedBaseAsync();

        var cmd    = new RestoreCategoryCommand(budgetId, group.Id, Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_NOT_FOUND");
    }
}
