using MyBudget.Features.Features.BudgetStructure.UpdateCategoryGroup;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCategoryGroup;

public sealed class UpdateCategoryGroupHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateCategoryGroupHandler _sut;

    public UpdateCategoryGroupHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateCategoryGroupHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_CATEGORY_GROUP_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var deleted = CategoryGroup.Create(budgetId, "Transport", 1);
        deleted.SoftDelete();
        _db.CategoryGroups.Add(deleted);

        var target = CategoryGroup.Create(budgetId, "Housing", 2);
        _db.CategoryGroups.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateCategoryGroupCommand(budgetId, target.Id, "Transport", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var target   = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateCategoryGroupCommand(budgetId, target.Id, "Housing", 1);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveSiblingDuplicate_Returns_CATEGORY_GROUP_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        _db.CategoryGroups.Add(CategoryGroup.Create(budgetId, "Transport", 1));
        var target = CategoryGroup.Create(budgetId, "Housing", 2);
        _db.CategoryGroups.Add(target);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateCategoryGroupCommand(budgetId, target.Id, "Transport", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }
}
