using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;

public sealed class CreateExecutionRecordHandler : IRequestHandler<CreateExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateExecutionRecordCommand cmd, CancellationToken ct)
    {
        // Load BudgetLine (budget-scoped; no PeriodId FK)
        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.Id == cmd.BudgetLineId && l.BudgetId == cmd.BudgetId,
                ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-EXEC-CREATE-2: BudgetLine soft-deleted guard
        if (line.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        // Load Period directly (BudgetLine no longer carries Period nav)
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period?.Cycle is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-EXEC-7: BudgetLine must overlap the period (date-range intersection check).
        // Overlap rule: BudgetLine has no EndDate OR EndDate >= Period.StartDate
        //   AND BudgetLine.StartDate <= Period.EndDate.
        // A line that ends before the period starts has no overlap → BUDGET_LINE_NOT_IN_PERIOD.
        var lineOverlapsPeriod = line.StartDate <= period.EndDate &&
            (line.EndDate is null || line.EndDate >= period.StartDate);
        if (!lineOverlapsPeriod)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_IN_PERIOD");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // REQ-EXEC-DATE-RANGE-1: OperationDate must fall within combined range:
        //   MAX(Period.StartDate, BudgetLine.StartDate) .. MIN(Period.EndDate, BudgetLine.EndDate ?? Period.EndDate)
        if (cmd.OperationDate.HasValue)
        {
            var effectiveStart = period.StartDate > line.StartDate ? period.StartDate : line.StartDate;
            var effectiveEnd   = line.EndDate.HasValue
                ? (period.EndDate < line.EndDate.Value ? period.EndDate : line.EndDate.Value)
                : period.EndDate;

            if (cmd.OperationDate.Value < effectiveStart || cmd.OperationDate.Value > effectiveEnd)
                return Result<Guid>.Failure("BUDGET_LINE_NOT_IN_PERIOD");
        }

        // REQ-EXEC-5/REQ-EXEC-6: ExchangeRate pair rule
        var defaultCurrencyId = period.Cycle.DefaultCurrencyId;
        var isSameCurrency = cmd.CurrencyId == defaultCurrencyId;

        if (isSameCurrency && (cmd.ExchangeRate != null || cmd.ExchangeRateTo != null))
            return Result<Guid>.Failure("EXCHANGE_RATE_NOT_ALLOWED");

        if (!isSameCurrency && (cmd.ExchangeRate == null || cmd.ExchangeRateTo == null))
            return Result<Guid>.Failure("EXCHANGE_RATE_PAIR_INCOMPLETE");

        var record = ExecutionRecord.Create(
            cmd.BudgetId,
            cmd.PeriodId,
            cmd.BudgetLineId,
            cmd.EntryType,
            cmd.Amount,
            cmd.Note,
            cmd.CurrencyId,
            cmd.ExchangeRate,
            cmd.ExchangeRateTo,
            cmd.AccountId,
            cmd.PaymentMethodId,
            cmd.OperationDate);

        _db.ExecutionRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(record.Id);
    }
}
