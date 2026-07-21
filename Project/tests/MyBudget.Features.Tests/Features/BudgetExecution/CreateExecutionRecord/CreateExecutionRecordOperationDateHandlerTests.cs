using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.CreateExecutionRecord;

/// <summary>Tests for REQ-EXEC-DATE-RANGE-1: OperationDate must fall within Period range.</summary>
public sealed class CreateExecutionRecordOperationDateHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateExecutionRecordHandler _sut;

    private static readonly DateOnly PeriodStart = new(2026, 1, 1);
    private static readonly DateOnly PeriodEnd   = new(2026, 1, 31);

    public CreateExecutionRecordOperationDateHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, Guid lineId)> SeedAsync()
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

        return (budgetId, period.Id, line.Id);
    }

    private static CreateExecutionRecordCommand BuildCmd(
        Guid budgetId, Guid periodId, Guid lineId, DateOnly? operationDate) =>
        new(budgetId, periodId, lineId,
            EntryType:       EntryType.Expense,
            Amount:          100m,
            Note:            "test note",
            CurrencyId:      CurrencySeeds.GtqId,
            ExchangeRate:    null,
            ExchangeRateTo:  null,
            AccountId:       null,
            PaymentMethodId: null,
            OperationDate:   operationDate);

    [Fact]
    public async Task OperationDate_WithinRange_Passes()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2026, 1, 15));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OperationDate_EqualToStartDate_Passes()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, PeriodStart);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OperationDate_EqualToEndDate_Passes()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, PeriodEnd);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OperationDate_BeforeStart_Returns_BUDGET_LINE_NOT_IN_PERIOD()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2025, 12, 31));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_IN_PERIOD");
    }

    [Fact]
    public async Task OperationDate_AfterEnd_Returns_BUDGET_LINE_NOT_IN_PERIOD()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2026, 2, 1));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_IN_PERIOD");
    }

    [Fact]
    public async Task OperationDate_Null_Passes()
    {
        var (budgetId, periodId, lineId) = await SeedAsync();
        var cmd    = BuildCmd(budgetId, periodId, lineId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
