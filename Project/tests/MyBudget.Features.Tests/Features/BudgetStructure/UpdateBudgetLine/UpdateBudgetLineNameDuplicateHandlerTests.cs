using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

/// <summary>Tests for REQ-BL-NAME-1 in update path: budget line name uniqueness scoped to BudgetId.</summary>
public sealed class UpdateBudgetLineNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateBudgetLineHandler _sut;

    public UpdateBudgetLineNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateBudgetLineHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid groupId)> SeedAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, group.Id);
    }

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        var (budgetId, groupId) = await SeedAsync();

        var deleted = BudgetLine.Create(budgetId, groupId, null, "Utilities", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        deleted.SoftDelete();
        _db.BudgetLines.Add(deleted);

        var target = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        var cmd = new UpdateBudgetLineCommand(
            budgetId, target.Id,
            groupId, null, "Utilities", LineType.Expense,
            null, null, 500m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var (budgetId, groupId) = await SeedAsync();

        var target = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        var cmd = new UpdateBudgetLineCommand(
            budgetId, target.Id,
            groupId, null, "Rent", LineType.Expense,
            null, null, 600m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveSiblingDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        var (budgetId, groupId) = await SeedAsync();

        var sibling = BudgetLine.Create(budgetId, groupId, null, "Utilities", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(sibling);

        var target = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        var cmd = new UpdateBudgetLineCommand(
            budgetId, target.Id,
            groupId, null, "Utilities", LineType.Expense,
            null, null, 500m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }
}
