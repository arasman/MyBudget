using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

public sealed class CreateBudgetLineHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateBudgetLineHandler _sut;

    public CreateBudgetLineHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateBudgetLineHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId)> SeedOpenPeriodAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)));
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id);
    }

    private async Task<(Guid budgetId, Guid periodId)> SeedClosedPeriodAsync()
    {
        var (budgetId, periodId) = await SeedOpenPeriodAsync();
        var period = await _db.Periods.FindAsync(periodId);
        period!.SetClosed(true);
        await _db.SaveChangesAsync();
        return (budgetId, periodId);
    }

    [Fact]
    public async Task ClosedPeriod_Returns_PERIOD_CLOSED()
    {
        var (budgetId, periodId) = await SeedClosedPeriodAsync();
        var cmd = new CreateBudgetLineCommand(
            budgetId, periodId, Guid.NewGuid(), null,
            "Rent", LineType.Expense, true, 1500m, "GTQ");

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task OpenPeriod_CreatesLineAndInitialRevision()
    {
        var (budgetId, periodId) = await SeedOpenPeriodAsync();

        // CategoryGroup must exist due to FK constraint
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var cmd = new CreateBudgetLineCommand(
            budgetId, periodId, group.Id, null,
            "Rent", LineType.Expense, true, 1500m, "GTQ");

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Verify BudgetLine was created
        var line = await _db.BudgetLines.FindAsync(result.Value);
        line.ShouldNotBeNull();
        line.Name.ShouldBe("Rent");
        line.LineType.ShouldBe(LineType.Expense);

        // Verify initial BudgetLineRevision was created
        var revision = await _db.BudgetLineRevisions
            .FirstOrDefaultAsync(r => r.BudgetLineId == result.Value);
        revision.ShouldNotBeNull();
        revision.BudgetedAmount.ShouldBe(1500m);
        revision.Currency.ShouldBe("GTQ");
    }

    [Fact]
    public async Task PeriodNotFound_Returns_Failure()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var cmd = new CreateBudgetLineCommand(
            budgetId, Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, false, 500m, "USD");

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_NOT_FOUND");
    }
}
