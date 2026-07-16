using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;

public sealed class UpdateExecutionRecordHandler : IRequestHandler<UpdateExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateExecutionRecordCommand cmd, CancellationToken ct)
    {
        // Load non-deleted ExecutionRecord with BudgetLine -> Period -> Cycle
        var record = await _db.ExecutionRecords
            .Include(e => e.BudgetLine)
                .ThenInclude(bl => bl!.Period)
                    .ThenInclude(p => p!.Cycle)
            .FirstOrDefaultAsync(
                e => e.Id == cmd.ExecutionId
                  && e.BudgetLineId == cmd.BudgetLineId
                  && e.PeriodId == cmd.PeriodId
                  && e.BudgetId == cmd.BudgetId,
                ct);

        if (record is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        var period = record.BudgetLine?.Period;
        var cycle  = period?.Cycle;

        if (period is null || cycle is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // REQ-EXEC-5/REQ-EXEC-6: ExchangeRate pair rule
        var defaultCurrencyId = cycle.DefaultCurrencyId;
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
