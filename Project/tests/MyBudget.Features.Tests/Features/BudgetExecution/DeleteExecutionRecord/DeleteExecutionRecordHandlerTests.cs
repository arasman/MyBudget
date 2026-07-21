using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.Features.BudgetExecution.DeleteExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.DeleteExecutionRecord;

public sealed class DeleteExecutionRecordHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DeleteExecutionRecordHandler _sut;
    private readonly CreateExecutionRecordHandler _createHandler;

    public DeleteExecutionRecordHandlerTests()
    {
        _db            = DbTestHelpers.CreateSqliteContext();
        _sut           = new DeleteExecutionRecordHandler(_db);
        _createHandler = new CreateExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid defaultCurrencyId, Guid periodId, Guid lineId)>
        SeedAsync(bool periodClosed = false)
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
        if (periodClosed) period.SetClosed(true);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            DateOnly.FromDateTime(DateTime.UtcNow), null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, cycle.DefaultCurrencyId, period.Id, line.Id);
    }

    private async Task<Guid> CreateRecordAsync(Guid budgetId, Guid periodId, Guid lineId, Guid currencyId)
    {
        var createCmd = new CreateExecutionRecordCommand(
            budgetId, periodId, lineId, EntryType.Expense, 100m, null,
            currencyId, null, null, null, null);
        var createResult = await _createHandler.Handle(createCmd, CancellationToken.None);
        createResult.IsSuccess.ShouldBeTrue();
        return createResult.Value;
    }

    [Fact]
    public async Task ValidDelete_Sets_DeletedAt()
    {
        var (budgetId, currencyId, periodId, lineId) = await SeedAsync();
        var executionId = await CreateRecordAsync(budgetId, periodId, lineId, currencyId);

        var cmd    = new DeleteExecutionRecordCommand(budgetId, periodId, lineId, executionId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Verify soft-deleted (IgnoreQueryFilters to see it)
        var record = await _db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == executionId);
        record.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task AlreadyDeleted_Returns_Not_Found()
    {
        var (budgetId, currencyId, periodId, lineId) = await SeedAsync();
        var executionId = await CreateRecordAsync(budgetId, periodId, lineId, currencyId);

        // First delete
        var cmd = new DeleteExecutionRecordCommand(budgetId, periodId, lineId, executionId);
        await _sut.Handle(cmd, CancellationToken.None);

        // Second delete — should return not found
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXECUTION_RECORD_NOT_FOUND");
    }

    [Fact]
    public async Task ClosedPeriod_Returns_PERIOD_CLOSED()
    {
        // Seed open, create record, then close the period
        var (budgetId, currencyId, periodId, lineId) = await SeedAsync();
        var executionId = await CreateRecordAsync(budgetId, periodId, lineId, currencyId);

        var period = await _db.Periods.FindAsync(periodId);
        period!.SetClosed(true);
        await _db.SaveChangesAsync();

        var cmd    = new DeleteExecutionRecordCommand(budgetId, periodId, lineId, executionId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task NonExistentRecord_Returns_Not_Found()
    {
        var (budgetId, _, periodId, lineId) = await SeedAsync();
        var cmd    = new DeleteExecutionRecordCommand(budgetId, periodId, lineId, Guid.NewGuid());
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXECUTION_RECORD_NOT_FOUND");
    }
}
