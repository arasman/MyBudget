using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.RestoreBudgetLine;

public sealed class RestoreBudgetLineHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RestoreBudgetLineHandler _sut;

    public RestoreBudgetLineHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new RestoreBudgetLineHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid lineId)> SeedSoftDeletedLineAsync(string name = "Rent")
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, group.Id, null, name, LineType.Expense,
            DateOnly.MinValue, null, 1000m, CurrencySeeds.GtqId, 1);
        line.SoftDelete();
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, line.Id);
    }

    [Fact]
    public async Task SingleRestore_RestoresBudgetLine()
    {
        var (budgetId, lineId) = await SeedSoftDeletedLineAsync();

        var cmd    = new RestoreBudgetLineCommand(budgetId, lineId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var line = await _db.BudgetLines.IgnoreQueryFilters().FirstAsync(bl => bl.Id == lineId);
        line.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task LineNotFound_Returns_BUDGET_LINE_NOT_FOUND()
    {
        var (budgetId, _) = await SeedSoftDeletedLineAsync();

        var cmd    = new RestoreBudgetLineCommand(budgetId, Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }
}
