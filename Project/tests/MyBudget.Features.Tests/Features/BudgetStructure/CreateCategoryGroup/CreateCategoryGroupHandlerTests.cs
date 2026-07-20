using MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCategoryGroup;

public sealed class CreateCategoryGroupHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateCategoryGroupHandler _sut;

    public CreateCategoryGroupHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateCategoryGroupHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ActiveDuplicate_Returns_CATEGORY_GROUP_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var existing = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(existing);
        await _db.SaveChangesAsync();

        var cmd    = new CreateCategoryGroupCommand(budgetId, "Housing", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedDuplicate_Returns_CATEGORY_GROUP_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var existing = CategoryGroup.Create(budgetId, "Housing", 1);
        existing.SoftDelete();
        _db.CategoryGroups.Add(existing);
        await _db.SaveChangesAsync();

        var cmd    = new CreateCategoryGroupCommand(budgetId, "Housing", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cmd    = new CreateCategoryGroupCommand(budgetId, "Transport", 1);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CaseInsensitiveDuplicate_Returns_CATEGORY_GROUP_NAME_DUPLICATE()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        _db.CategoryGroups.Add(CategoryGroup.Create(budgetId, "housing", 1));
        await _db.SaveChangesAsync();

        var cmd    = new CreateCategoryGroupCommand(budgetId, "HOUSING", 2);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }
}
