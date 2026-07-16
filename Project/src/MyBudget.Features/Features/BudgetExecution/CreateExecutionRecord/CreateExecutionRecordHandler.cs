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
        // Load BudgetLine (with IgnoreQueryFilters to detect soft-deleted parent) -> Period -> Cycle
        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Include(l => l.Period)
                .ThenInclude(p => p!.Cycle)
            .FirstOrDefaultAsync(
                l => l.Id == cmd.BudgetLineId && l.PeriodId == cmd.PeriodId && l.BudgetId == cmd.BudgetId,
                ct);

        if (line is null || line.Period is null || line.Period.Cycle is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-EXEC-7: route PeriodId must match BudgetLine.PeriodId
        if (line.PeriodId != cmd.PeriodId)
            return Result<Guid>.Failure("PERIOD_MISMATCH");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (line.Period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // REQ-EXEC-CREATE-2: BudgetLine soft-deleted guard
        if (line.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        // REQ-EXEC-5/REQ-EXEC-6: ExchangeRate pair rule
        var defaultCurrencyId = line.Period.Cycle.DefaultCurrencyId;
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
