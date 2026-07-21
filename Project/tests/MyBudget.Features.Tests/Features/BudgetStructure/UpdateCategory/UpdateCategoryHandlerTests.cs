using MyBudget.Features.Features.BudgetStructure.UpdateCategory;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCategory;

public sealed class UpdateCategoryHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateCategoryHandler _sut;

    public UpdateCategoryHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateCategoryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid groupId)> SeedGroupAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var group    = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();
        return (budgetId, group.Id);
    }

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_CATEGORY_NAME_DUPLICATE()
    {
        var (budgetId, groupId) = await SeedGroupAsync();

        var deleted = Category.Create(budgetId, groupId, "Utilities", 1);
        deleted.SoftDelete();
        _db.Categories.Add(deleted);

        var target = Category.Create(budgetId, groupId, "Rent", 2);
        _db.Categories.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateCategoryCommand(budgetId, groupId, target.Id, "Utilities", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var (budgetId, groupId) = await SeedGroupAsync();
        var target = Category.Create(budgetId, groupId, "Rent", 1);
        _db.Categories.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateCategoryCommand(budgetId, groupId, target.Id, "Rent", 1);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
