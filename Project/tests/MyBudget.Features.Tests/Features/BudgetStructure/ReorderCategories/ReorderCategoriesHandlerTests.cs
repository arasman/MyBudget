using MyBudget.Features.Features.BudgetStructure.ReorderCategories;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.ReorderCategories;

public sealed class ReorderCategoriesHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReorderCategoriesHandler _sut;

    public ReorderCategoriesHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new ReorderCategoriesHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task IncompleteList_Returns_REORDER_LIST_INCOMPLETE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var catA = Category.Create(group.Id, "Rent", 1);
        var catB = Category.Create(group.Id, "Utilities", 2);
        var catC = Category.Create(group.Id, "Insurance", 3);
        _db.Categories.AddRange(catA, catB, catC);
        await _db.SaveChangesAsync();

        // Only 2 of 3 IDs — incomplete
        var cmd = new ReorderCategoriesCommand(budgetId, group.Id, [catA.Id, catB.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_LIST_INCOMPLETE");
    }

    [Fact]
    public async Task DuplicateIds_Returns_REORDER_LIST_INVALID()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var catA = Category.Create(group.Id, "Rent", 1);
        var catB = Category.Create(group.Id, "Utilities", 2);
        _db.Categories.AddRange(catA, catB);
        await _db.SaveChangesAsync();

        var cmd = new ReorderCategoriesCommand(budgetId, group.Id, [catA.Id, catA.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_LIST_INVALID");
    }

    [Fact]
    public async Task ValidReorder_AssignsCorrectDisplayOrder()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var catA = Category.Create(group.Id, "Rent", 1);
        var catB = Category.Create(group.Id, "Utilities", 2);
        _db.Categories.AddRange(catA, catB);
        await _db.SaveChangesAsync();

        // Reverse order: B=1, A=2
        var cmd = new ReorderCategoriesCommand(budgetId, group.Id, [catB.Id, catA.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _db.Entry(catB).ReloadAsync();
        await _db.Entry(catA).ReloadAsync();

        catB.DisplayOrder.ShouldBe(1);
        catA.DisplayOrder.ShouldBe(2);
    }
}
