using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteBudgetLine;

// TODO PR4: full rewrite — remove period-closed guard, adapt to budget-scoped delete
public sealed class DeleteBudgetLineHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DeleteBudgetLineHandler _sut;

    public DeleteBudgetLineHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new DeleteBudgetLineHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid lineId)> SeedLineAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            DateOnly.FromDateTime(DateTime.UtcNow), null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, line.Id);
    }

    [Fact]
    public async Task HappyPath_SoftDeletesLine()
    {
        // TODO PR4: full test rewrite — stub to compile only
        var (budgetId, lineId) = await SeedLineAsync();
        var cmd = new DeleteBudgetLineCommand(budgetId, lineId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .FirstAsync(l => l.Id == lineId);
        line.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task LineNotFound_Returns_Failure()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var cmd = new DeleteBudgetLineCommand(budgetId, Guid.NewGuid());

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }
}
