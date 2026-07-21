using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.CreateExecutionRecord;

/// <summary>
/// Tests for REQ-EXEC-DATE-RANGE-1 (combined range) and REQ-EXEC-7 (BudgetLine covers period).
/// OperationDate must fall within MAX(Period.StartDate, BudgetLine.StartDate)
///   .. MIN(Period.EndDate, BudgetLine.EndDate ?? Period.EndDate).
/// BudgetLine must cover the period via date-range intersection (no PeriodId FK).
/// </summary>
public sealed class CreateExecutionRecordBudgetLineDateRangeHandlerTests : IDisposable
{
    private static readonly DateOnly PeriodStart = new(2025, 1, 1);
    private static readonly DateOnly PeriodEnd   = new(2025, 1, 31);

    private readonly AppDbContext _db;
    private readonly CreateExecutionRecordHandler _sut;

    public CreateExecutionRecordBudgetLineDateRangeHandlerTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateExecutionRecordHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid periodId, Guid lineId)> SeedAsync(
        DateOnly lineStart, DateOnly? lineEnd)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var cycle = Cycle.Create(budgetId, "Test Cycle",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CurrencySeeds.GtqId);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1, PeriodStart, PeriodEnd);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            lineStart, lineEnd, 1000m, CurrencySeeds.GtqId);
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

    // ── REQ-EXEC-7: BudgetLine must cover the period via date-range ──────────────

    [Fact]
    public async Task BudgetLine_StartsBeforePeriod_Perpetual_Covers_Period()
    {
        // BudgetLine covers period: StartDate=2024-01-01, EndDate=null
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2024, 1, 1), null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task BudgetLine_EndedBeforePeriodStart_Returns_BUDGET_LINE_NOT_IN_PERIOD()
    {
        // BudgetLine.EndDate=2024-12-31 < Period.StartDate=2025-01-01 → no overlap
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        var cmd    = BuildCmd(budgetId, periodId, lineId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_IN_PERIOD");
    }

    [Fact]
    public async Task BudgetLine_ExactPeriodStart_Covers_Period()
    {
        // BudgetLine.StartDate=2025-01-01, EndDate=null → covers exactly from period start
        var (budgetId, periodId, lineId) = await SeedAsync(PeriodStart, null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task BudgetLine_StartAfterPeriodStart_PeriodEndCoversLineStart_Overlaps_Period()
    {
        // BudgetLine.StartDate=2025-01-15 is mid-period but still overlaps Jan 1-31 period
        // Spec REQ-EXEC-DATE-RANGE-1 scenario expects this to proceed to OperationDate checks
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2025, 1, 15), null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, null); // null OperationDate skips range check
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    // ── REQ-EXEC-DATE-RANGE-1: combined OperationDate range ──────────────────────

    [Fact]
    public async Task OperationDate_BeforeBudgetLineStart_Returns_OPERATION_DATE_OUT_OF_RANGE()
    {
        // Period: Jan 1–31; BudgetLine starts Jan 15 → effective start = Jan 15
        // OperationDate = Jan 10 < Jan 15 → out of combined range
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2025, 1, 15), null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2025, 1, 10));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("OPERATION_DATE_OUT_OF_RANGE");
    }

    [Fact]
    public async Task OperationDate_WithinBudgetLineStartAndPeriod_Passes()
    {
        // Period: Jan 1–31; BudgetLine starts Jan 15 → effective start = Jan 15
        // OperationDate = Jan 20 → within combined range
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2025, 1, 15), null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2025, 1, 20));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OperationDate_AfterBudgetLineEnd_Returns_OPERATION_DATE_OUT_OF_RANGE()
    {
        // Period: Jan 1–31; BudgetLine ends Jan 20 → effective end = Jan 20
        // OperationDate = Jan 25 > Jan 20 → out of combined range
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 20));
        var cmd    = BuildCmd(budgetId, periodId, lineId, new DateOnly(2025, 1, 25));
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("OPERATION_DATE_OUT_OF_RANGE");
    }

    [Fact]
    public async Task OperationDate_Null_SkipsCombinedRangeCheck_Passes()
    {
        // BudgetLine starts mid-period; null OperationDate → no date-range validation
        var (budgetId, periodId, lineId) = await SeedAsync(new DateOnly(2025, 1, 15), null);
        var cmd    = BuildCmd(budgetId, periodId, lineId, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
