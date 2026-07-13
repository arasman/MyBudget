using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.DeleteExecutionRecord;

public sealed class DeleteExecutionRecordHandler : IRequestHandler<DeleteExecutionRecordCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteExecutionRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteExecutionRecordCommand cmd, CancellationToken ct)
    {
        // Load non-deleted ExecutionRecord with BudgetLine -> Period
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

        // REQ-EXEC-DELETE-2: already soft-deleted -> 404
        if (record is null)
            return Result<Guid>.Failure("EXECUTION_RECORD_NOT_FOUND");

        var period = record.BudgetLine?.Period;

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
