using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteBudgetLine;

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

    private async Task<(Guid budgetId, Guid periodId, Guid lineId)> SeedLineAsync(bool isClosed = false)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        if (isClosed) period.SetClosed(true);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        // CategoryGroup must exist due to FK constraint
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, period.Id, group.Id, null, "Rent", LineType.Expense, true);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, line.Id);
    }

    [Fact]
    public async Task ClosedPeriod_Returns_PERIOD_CLOSED()
    {
        var (budgetId, periodId, lineId) = await SeedLineAsync(isClosed: true);
        var cmd = new DeleteBudgetLineCommand(budgetId, periodId, lineId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task OpenPeriod_SoftDeletesLine()
    {
        var (budgetId, periodId, lineId) = await SeedLineAsync(isClosed: false);
        var cmd = new DeleteBudgetLineCommand(budgetId, periodId, lineId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // BudgetLine must be soft-deleted (use IgnoreQueryFilters to see it)
        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .FirstAsync(l => l.Id == lineId);
        line.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task LineNotFound_Returns_Failure()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var cmd = new DeleteBudgetLineCommand(budgetId, Guid.NewGuid(), Guid.NewGuid());

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }
}
