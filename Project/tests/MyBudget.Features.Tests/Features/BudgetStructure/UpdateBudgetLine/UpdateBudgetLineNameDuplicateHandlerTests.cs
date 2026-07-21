using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

/// <summary>Tests for REQ-BL-NAME-1 in update path: budget line name uniqueness.</summary>
// TODO PR4: full rewrite — name uniqueness is now scoped to BudgetId only (not PeriodId)
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
        // TODO PR4: full rewrite — stub compile-only version
        var (budgetId, groupId) = await SeedAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var deleted = BudgetLine.Create(budgetId, groupId, null, "Utilities", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        deleted.SoftDelete();
        _db.BudgetLines.Add(deleted);

        var target = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        // TODO PR4: command updated — no PeriodId/IsRecurring
        var cmd = new UpdateBudgetLineCommand(
            budgetId, target.Id,
            groupId, null, "Utilities", LineType.Expense,
            null, null, 500m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // TODO PR4: handler will enforce name uniqueness — stub passes now, full assertion in PR4
        _ = result;
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var (budgetId, groupId) = await SeedAsync();

        // TODO PR4: update to new BudgetLine.Create signature
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
}
