using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

/// <summary>Tests for REQ-BL-NAME-1 in update path: budget line name uniqueness.</summary>
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

    private async Task<(Guid budgetId, Guid periodId, Guid groupId)> SeedAsync()
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

        return (budgetId, period.Id, group.Id);
    }

    [Fact]
    public async Task SoftDeletedSiblingDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        var (budgetId, periodId, groupId) = await SeedAsync();

        var deleted = BudgetLine.Create(budgetId, periodId, groupId, null, "Utilities", LineType.Expense, true);
        deleted.SoftDelete();
        _db.BudgetLines.Add(deleted);

        var target = BudgetLine.Create(budgetId, periodId, groupId, null, "Rent", LineType.Expense, false);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        var cmd = new UpdateBudgetLineCommand(
            budgetId, periodId, target.Id,
            groupId, null, "Utilities", LineType.Expense, false, 500m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SelfRename_Succeeds()
    {
        var (budgetId, periodId, groupId) = await SeedAsync();

        var target = BudgetLine.Create(budgetId, periodId, groupId, null, "Rent", LineType.Expense, true);
        _db.BudgetLines.Add(target);
        await _db.SaveChangesAsync();

        var cmd = new UpdateBudgetLineCommand(
            budgetId, periodId, target.Id,
            groupId, null, "Rent", LineType.Expense, true, 600m, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
