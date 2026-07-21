using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

/// <summary>Tests for REQ-BL-NAME-1: budget line name uniqueness per (period, categoryGroup, category).</summary>
public sealed class CreateBudgetLineNameDuplicateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateBudgetLineHandler _sut;

    public CreateBudgetLineNameDuplicateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateBudgetLineHandler(_db);
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
    public async Task ActiveDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        var (budgetId, periodId, groupId) = await SeedAsync();

        _db.BudgetLines.Add(BudgetLine.Create(budgetId, periodId, groupId, null, "Rent", LineType.Expense, true));
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, periodId, groupId, null,
            "Rent", LineType.Expense, false, 500m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        var (budgetId, periodId, groupId) = await SeedAsync();

        var deleted = BudgetLine.Create(budgetId, periodId, groupId, null, "Rent", LineType.Expense, true);
        deleted.SoftDelete();
        _db.BudgetLines.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, periodId, groupId, null,
            "Rent", LineType.Expense, false, 500m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        var (budgetId, periodId, groupId) = await SeedAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, periodId, groupId, null,
            "Utilities", LineType.Expense, false, 200m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
