using MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.UpdateExecutionRecord;

/// <summary>Tests for REQ-EXEC-DATE-RANGE-1 in update path: OperationDate must fall within Period range.</summary>
public sealed class UpdateExecutionRecordOperationDateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UpdateExecutionRecordHandler _sut;

    private static readonly DateOnly PeriodStart = new(2026, 1, 1);
    private static readonly DateOnly PeriodEnd   = new(2026, 1, 31);

    public UpdateExecutionRecordOperationDateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, Guid lineId, Guid recordId)> SeedAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1, PeriodStart, PeriodEnd);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            PeriodStart, null, 1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        var record = ExecutionRecord.Create(
            budgetId, period.Id, line.Id,
            EntryType.Expense, 100m, "initial note",
            CurrencySeeds.GtqId, null, null, null, null, null);
        _db.ExecutionRecords.Add(record);
        await _db.SaveChangesAsync();

        return (budgetId, period.Id, line.Id, record.Id);
    }

    private static UpdateExecutionRecordCommand BuildCmd(
        Guid budgetId, Guid periodId, Guid lineId, Guid recordId, DateOnly? operationDate) =>
        new(budgetId, periodId, lineId, recordId,
            EntryType:       EntryType.Expense,
            Amount:          150m,
            Note:            "updated note",
            CurrencyId:      CurrencySeeds.GtqId,
            ExchangeRate:    null,
            ExchangeRateTo:  null,
            AccountId:       null,
            PaymentMethodId: null,
            OperationDate:   operationDate);

    [Fact]
    public async Task OperationDate_BeforeStart_Returns_OPERATION_DATE_OUT_OF_RANGE()
    {
        var (budgetId, periodId, lineId, recordId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, recordId, new DateOnly(2025, 12, 31));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("OPERATION_DATE_OUT_OF_RANGE");
    }

    [Fact]
    public async Task OperationDate_AfterEnd_Returns_OPERATION_DATE_OUT_OF_RANGE()
    {
        var (budgetId, periodId, lineId, recordId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, recordId, new DateOnly(2026, 2, 1));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("OPERATION_DATE_OUT_OF_RANGE");
    }

    [Fact]
    public async Task OperationDate_WithinRange_Passes()
    {
        var (budgetId, periodId, lineId, recordId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, recordId, new DateOnly(2026, 1, 15));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OperationDate_Null_Passes()
    {
        var (budgetId, periodId, lineId, recordId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, recordId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
