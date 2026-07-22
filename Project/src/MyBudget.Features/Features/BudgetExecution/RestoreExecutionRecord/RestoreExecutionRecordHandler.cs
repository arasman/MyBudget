using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.RestoreExecutionRecord;

/// <summary>
/// Restores a soft-deleted ExecutionRecord.
/// REQ-EXEC-RESTORE-1: load soft-deleted record, check IsClosed, restore.
/// REQ-EXEC-RESTORE-2: non-deleted record -> 404.
/// REQ-EXEC-CLOSED-1: IsClosed -> PERIOD_CLOSED 409.
/// REQ-EXEC-RESTORE-DATERANGE-1: Period date range must fall within BudgetLine [StartDate, EndDate].
/// </summary>
public sealed class RestoreExecutionRecordHandler : IRequestHandler<RestoreExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreExecutionRecordCommand cmd, CancellationToken ct)
    {
        // REQ-EXEC-RESTORE-2: a non-deleted record is NOT found here -> 404
        var record = await _db.ExecutionRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.Id            == cmd.ExecutionId
                  && e.BudgetLineId  == cmd.BudgetLineId
                  && e.PeriodId      == cmd.PeriodId
                  && e.BudgetId      == cmd.BudgetId
                  && e.DeletedAt     != null,
                ct);

        if (record is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // Load Period directly (BudgetLine no longer carries Period nav)
        var period = await _db.Periods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // REQ-EXEC-RESTORE-DATERANGE-1: period date range must fall within BudgetLine [StartDate, EndDate]
        var budgetLine = await _db.BudgetLines
            .FirstOrDefaultAsync(bl => bl.Id == cmd.BudgetLineId, ct);

        if (budgetLine is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        var outsideRange =
            period.StartDate < budgetLine.StartDate ||
            (budgetLine.EndDate.HasValue && period.EndDate > budgetLine.EndDate.Value);

        if (outsideRange)
            return Result<Guid>.Failure("EXECUTION_OUT_OF_DATE_RANGE");

        record.Restore();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(record.Id);
    }
}
