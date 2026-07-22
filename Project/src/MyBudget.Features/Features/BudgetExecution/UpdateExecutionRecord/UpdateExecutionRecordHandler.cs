using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;

// TODO PR2b: full handler rewrite — load Period and Cycle directly (BudgetLine no longer has Period nav)
public sealed class UpdateExecutionRecordHandler : IRequestHandler<UpdateExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateExecutionRecordCommand cmd, CancellationToken ct)
    {
        var record = await _db.ExecutionRecords
            .FirstOrDefaultAsync(
                e => e.Id == cmd.ExecutionId
                  && e.BudgetLineId == cmd.BudgetLineId
                  && e.PeriodId == cmd.PeriodId
                  && e.BudgetId == cmd.BudgetId,
                ct);

        if (record is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // Load Period and Cycle directly
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period?.Cycle is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // REQ-EXEC-DATE-RANGE-1: OperationDate must fall within Period range (null = skip check)
        if (cmd.OperationDate.HasValue &&
            (cmd.OperationDate.Value < period.StartDate || cmd.OperationDate.Value > period.EndDate))
            return Result<Guid>.Failure("OPERATION_DATE_OUT_OF_RANGE");

        // REQ-EXEC-5/REQ-EXEC-6: ExchangeRate pair rule
        var defaultCurrencyId = period.Cycle.DefaultCurrencyId;
        var isSameCurrency = cmd.CurrencyId == defaultCurrencyId;

        if (isSameCurrency && (cmd.ExchangeRate != null || cmd.ExchangeRateTo != null))
            return Result<Guid>.Failure("EXCHANGE_RATE_NOT_ALLOWED");

        if (!isSameCurrency && (cmd.ExchangeRate == null || cmd.ExchangeRateTo == null))
            return Result<Guid>.Failure("EXCHANGE_RATE_PAIR_INCOMPLETE");

        record.Update(
            cmd.EntryType,
            cmd.Amount,
            cmd.Note,
            cmd.CurrencyId,
            cmd.ExchangeRate,
            cmd.ExchangeRateTo,
            cmd.AccountId,
            cmd.PaymentMethodId,
            cmd.OperationDate);

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(record.Id);
    }
}
