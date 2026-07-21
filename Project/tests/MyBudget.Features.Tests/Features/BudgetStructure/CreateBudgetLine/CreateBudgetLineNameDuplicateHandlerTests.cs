using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

/// <summary>Tests for REQ-BL-NAME-1: budget line name uniqueness per Budget.</summary>
// TODO PR4: full rewrite — name uniqueness is now scoped to BudgetId only (not PeriodId)
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

    private async Task<(Guid budgetId, Guid groupId)> SeedAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        return (budgetId, group.Id);
    }

    [Fact]
    public async Task ActiveDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        // TODO PR4: full rewrite — stub compile-only version
        var (budgetId, groupId) = await SeedAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        _db.BudgetLines.Add(BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId));
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, groupId, null,
            "Rent", LineType.Expense, new DateOnly(2025, 1, 1), null, 500m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task SoftDeletedDuplicate_Returns_BUDGET_LINE_NAME_DUPLICATE()
    {
        // TODO PR4: full rewrite — stub compile-only version
        var (budgetId, groupId) = await SeedAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var deleted = BudgetLine.Create(budgetId, groupId, null, "Rent", LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId);
        deleted.SoftDelete();
        _db.BudgetLines.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, groupId, null,
            "Rent", LineType.Expense, new DateOnly(2025, 1, 1), null, 500m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NAME_DUPLICATE");
    }

    [Fact]
    public async Task UniqueName_Succeeds()
    {
        // TODO PR4: full rewrite — stub compile-only version
        var (budgetId, groupId) = await SeedAsync();

        var cmd    = new CreateBudgetLineCommand(budgetId, groupId, null,
            "Utilities", LineType.Expense, new DateOnly(2025, 1, 1), null, 200m, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
