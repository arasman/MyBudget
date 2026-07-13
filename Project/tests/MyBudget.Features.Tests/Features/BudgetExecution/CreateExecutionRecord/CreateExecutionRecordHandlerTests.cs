using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.CreateExecutionRecord;

public sealed class CreateExecutionRecordHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateExecutionRecordHandler _sut;

    public CreateExecutionRecordHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid cycleDefaultCurrencyId, Guid periodId, Guid lineId, Guid groupId)>
        SeedAsync(bool periodClosed = false, bool lineSoftDeleted = false)
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
        if (lineSoftDeleted) line.SoftDelete();
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync();

        return (budgetId, cycle.DefaultCurrencyId, period.Id, line.Id, group.Id);
    }

    private static CreateExecutionRecordCommand BuildCommand(
        Guid budgetId, Guid periodId, Guid lineId,
        Guid currencyId, decimal amount = 100m, string? note = null,
        EntryType entryType = EntryType.Expense,
        decimal? exchangeRate = null, decimal? exchangeRateTo = null) =>
        new(budgetId, periodId, lineId, entryType, amount, note,
            currencyId, exchangeRate, exchangeRateTo, null, null);

    [Fact]
    public async Task PeriodId_Mismatch_Returns_PERIOD_MISMATCH()
    {
        // We use a wrong periodId that does not match the BudgetLine's PeriodId
        var (budgetId, _, _, lineId, _) = await SeedAsync();
        var wrongPeriodId = Guid.NewGuid();

        // Note: the handler query won't find the line because l.PeriodId != cmd.PeriodId
        // so it returns BUDGET_LINE_NOT_FOUND (line not found under that periodId)
        // Per REQ-EXEC-7 + REQ-EXEC-CREATE-2, a mismatch or missing line yields 404
        var cmd = BuildCommand(budgetId, wrongPeriodId, lineId, CurrencySeeds.GtqId);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }

    [Fact]
    public async Task ClosedPeriod_Returns_PERIOD_CLOSED()
    {
        var (budgetId, _, periodId, lineId, _) = await SeedAsync(periodClosed: true);
        var cmd = BuildCommand(budgetId, periodId, lineId, CurrencySeeds.GtqId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task BudgetLine_SoftDeleted_Returns_PARENT_IS_DELETED()
    {
        var (budgetId, _, periodId, lineId, _) = await SeedAsync(lineSoftDeleted: true);
        var cmd = BuildCommand(budgetId, periodId, lineId, CurrencySeeds.GtqId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PARENT_IS_DELETED");
    }

    [Fact]
    public async Task ValidCommand_Persists_ExecutionRecord()
    {
        var (budgetId, defaultCurrencyId, periodId, lineId, _) = await SeedAsync();
        var cmd = BuildCommand(budgetId, periodId, lineId, defaultCurrencyId, amount: 250m);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);

        var record = await _db.ExecutionRecords.FindAsync(result.Value);
        record.ShouldNotBeNull();
        record.Amount.ShouldBe(250m);
        record.BudgetLineId.ShouldBe(lineId);
        record.PeriodId.ShouldBe(periodId);
        record.BudgetId.ShouldBe(budgetId);
    }

    [Fact]
    public async Task BudgetLineNotFound_Returns_Failure()
    {
        var (budgetId, _, periodId, _, _) = await SeedAsync();
        var cmd = BuildCommand(budgetId, periodId, Guid.NewGuid(), CurrencySeeds.GtqId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }

    [Fact]
    public async Task SameCurrency_ExchangeRateProvided_Returns_EXCHANGE_RATE_NOT_ALLOWED()
    {
        var (budgetId, defaultCurrencyId, periodId, lineId, _) = await SeedAsync();
        var cmd = BuildCommand(budgetId, periodId, lineId, defaultCurrencyId,
            exchangeRate: 7.5m, exchangeRateTo: 0.133m);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXCHANGE_RATE_NOT_ALLOWED");
    }

    [Fact]
    public async Task DifferentCurrency_MissingExchangeRate_Returns_EXCHANGE_RATE_PAIR_INCOMPLETE()
    {
        var (budgetId, _, periodId, lineId, _) = await SeedAsync();
        var cmd = BuildCommand(budgetId, periodId, lineId, CurrencySeeds.UsdId,
            exchangeRate: 7.5m); // ExchangeRateTo missing

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("EXCHANGE_RATE_PAIR_INCOMPLETE");
    }
}
