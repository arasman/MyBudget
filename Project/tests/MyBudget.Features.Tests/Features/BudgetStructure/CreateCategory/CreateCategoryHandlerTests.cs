using MyBudget.Features.Features.BudgetStructure.CreateCategory;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCategory;

public sealed class CreateCategoryHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateCategoryHandler _sut;

    public CreateCategoryHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateCategoryHandler(_db);
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
    public async Task SoftDeletedDuplicate_Returns_CATEGORY_NAME_DUPLICATE()
    {
        var (budgetId, groupId) = await SeedGroupAsync();

        var deleted = Category.Create(budgetId, groupId, "Rent", 1);
        deleted.SoftDelete();
        _db.Categories.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = new CreateCategoryCommand(budgetId, groupId, "Rent", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_NAME_DUPLICATE");
    }

    [Fact]
    public async Task ActiveDuplicate_Returns_CATEGORY_NAME_DUPLICATE()
    {
        var (budgetId, groupId) = await SeedGroupAsync();
        _db.Categories.Add(Category.Create(budgetId, groupId, "Rent", 1));
        await _db.SaveChangesAsync();

        var cmd    = new CreateCategoryCommand(budgetId, groupId, "Rent", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var (budgetId, groupId) = await SeedGroupAsync();

        var cmd    = new CreateCategoryCommand(budgetId, groupId, "Utilities", 1);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
    }
}
