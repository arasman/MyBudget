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

    private async Task<(Guid budgetId, Guid periodId, Guid lineId)> SeedLineAsync(bool periodSoftDeleted = false)
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

        if (periodSoftDeleted)
            period.SoftDelete();

        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, period.Id, group.Id, null, "Rent", LineType.Expense, true, 1);
        line.SoftDelete();
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, line.Id);
    }

    [Fact]
    public async Task SingleRestore_RestoresBudgetLine()
    {
        var (budgetId, periodId, lineId) = await SeedLineAsync(periodSoftDeleted: false);

        var cmd    = new RestoreBudgetLineCommand(budgetId, periodId, lineId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var line = await _db.BudgetLines.IgnoreQueryFilters().FirstAsync(bl => bl.Id == lineId);
        line.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ParentPeriodSoftDeleted_Returns_PARENT_IS_DELETED()
    {
        var (budgetId, periodId, lineId) = await SeedLineAsync(periodSoftDeleted: true);

        var cmd    = new RestoreBudgetLineCommand(budgetId, periodId, lineId, false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PARENT_IS_DELETED");
    }

    [Fact]
    public async Task LineNotFound_Returns_BUDGET_LINE_NOT_FOUND()
    {
        var (budgetId, periodId, _) = await SeedLineAsync(periodSoftDeleted: false);

        var cmd    = new RestoreBudgetLineCommand(budgetId, periodId, Guid.NewGuid(), false);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }
}
