using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.UpdateExecutionRecord;

public sealed class UpdateExecutionRecordHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateExecutionRecordHandler _sut;
    private readonly CreateExecutionRecordHandler _createHandler;

    public UpdateExecutionRecordHandlerTests()
    {
        _db            = DbTestHelpers.CreateSqliteContext();
        _sut           = new UpdateExecutionRecordHandler(_db);
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

        var line = BudgetLine.Create(budgetId, period.Id, group.Id, null, "Rent", LineType.Expense, true);
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
    public async Task ValidUpdate_Persists_Changes()
    {
        var (budgetId, currencyId, periodId, lineId) = await SeedAsync();
        var executionId = await CreateRecordAsync(budgetId, periodId, lineId, currencyId);

        var cmd = new UpdateExecutionRecordCommand(
            budgetId, periodId, lineId, executionId,
            EntryType.CreditNote, 50m, "refund",
            currencyId, null, null, null, null);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var record = await _db.ExecutionRecords.FindAsync(executionId);
        record.ShouldNotBeNull();
        record.Amount.ShouldBe(50m);
        record.EntryType.ShouldBe(EntryType.CreditNote);
        record.Note.ShouldBe("refund");
    }

    [Fact]
    public async Task NonExistentRecord_Returns_Not_Found()
    {
        var (budgetId, _, periodId, lineId) = await SeedAsync();

        var cmd = new UpdateExecutionRecordCommand(
            budgetId, periodId, lineId, Guid.NewGuid(),
            EntryType.Expense, 50m, null,
            CurrencySeeds.GtqId, null, null, null, null);

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

        var cmd = new UpdateExecutionRecordCommand(
            budgetId, periodId, lineId, executionId,
            EntryType.Expense, 75m, null,
            currencyId, null, null, null, null);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }
}
