using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.DeleteExecutionRecord;

// TODO PR2b: full handler rewrite — load Period directly (BudgetLine no longer has Period nav)
public sealed class DeleteExecutionRecordHandler : IRequestHandler<DeleteExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteExecutionRecordCommand cmd, CancellationToken ct)
    {
        // Load ExecutionRecord without BudgetLine->Period chain
        var record = await _db.ExecutionRecords
            .FirstOrDefaultAsync(
                e => e.Id == cmd.ExecutionId
                  && e.BudgetLineId == cmd.BudgetLineId
                  && e.PeriodId == cmd.PeriodId
                  && e.BudgetId == cmd.BudgetId,
                ct);

        if (record is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // Load Period directly for IsClosed guard
        var period = await _db.Periods
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        // REQ-EXEC-CLOSED-1: period closed guard
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        record.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(record.Id);
    }
}
