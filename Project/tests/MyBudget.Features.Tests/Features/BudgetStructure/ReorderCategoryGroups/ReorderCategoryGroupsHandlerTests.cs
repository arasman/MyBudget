using MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.ReorderCategoryGroups;

public sealed class ReorderCategoryGroupsHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReorderCategoryGroupsHandler _sut;

    public ReorderCategoryGroupsHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new ReorderCategoryGroupsHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task IncompleteList_Returns_REORDER_LIST_INCOMPLETE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var groupA = CategoryGroup.Create(budgetId, "GroupA", 1);
        var groupB = CategoryGroup.Create(budgetId, "GroupB", 2);
        var groupC = CategoryGroup.Create(budgetId, "GroupC", 3);
        _db.CategoryGroups.AddRange(groupA, groupB, groupC);
        await _db.SaveChangesAsync();

        // Only pass 2 of 3 IDs — incomplete
        var cmd = new ReorderCategoryGroupsCommand(budgetId, [groupA.Id, groupB.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_LIST_INCOMPLETE");
    }

    [Fact]
    public async Task DuplicateIds_Returns_REORDER_LIST_INVALID()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var groupA = CategoryGroup.Create(budgetId, "GroupA", 1);
        var groupB = CategoryGroup.Create(budgetId, "GroupB", 2);
        _db.CategoryGroups.AddRange(groupA, groupB);
        await _db.SaveChangesAsync();

        // Duplicate ID
        var cmd = new ReorderCategoryGroupsCommand(budgetId, [groupA.Id, groupA.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REORDER_LIST_INVALID");
    }

    [Fact]
    public async Task ValidReorder_AssignsCorrectDisplayOrder()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var groupA = CategoryGroup.Create(budgetId, "GroupA", 1);
        var groupB = CategoryGroup.Create(budgetId, "GroupB", 2);
        var groupC = CategoryGroup.Create(budgetId, "GroupC", 3);
        _db.CategoryGroups.AddRange(groupA, groupB, groupC);
        await _db.SaveChangesAsync();

        // Reorder: C=1, A=2, B=3
        var cmd = new ReorderCategoryGroupsCommand(budgetId, [groupC.Id, groupA.Id, groupB.Id]);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _db.Entry(groupC).ReloadAsync();
        await _db.Entry(groupA).ReloadAsync();
        await _db.Entry(groupB).ReloadAsync();

        groupC.DisplayOrder.ShouldBe(1);
        groupA.DisplayOrder.ShouldBe(2);
        groupB.DisplayOrder.ShouldBe(3);
    }
}
