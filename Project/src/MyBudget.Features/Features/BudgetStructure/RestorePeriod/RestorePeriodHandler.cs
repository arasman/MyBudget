using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestorePeriod;

public sealed class RestorePeriodHandler : IRequestHandler<RestorePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestorePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestorePeriodCommand cmd, CancellationToken ct)
    {
        var cycle = await _db.Cycles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.Id == cmd.CycleId && c.BudgetId == cmd.BudgetId,
                ct);

        if (cycle is null)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        if (cycle.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        var period = await _db.Periods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.Id == cmd.PeriodId && p.CycleId == cmd.CycleId && p.DeletedAt != null,
                ct);

        if (period is null)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        period.Restore();

        // REQ-RESTORE-PERIOD-1: BudgetLines are Budget-scoped (no PeriodId FK).
        // BudgetLines are NOT cascade-restored when a Period is restored (REQ-CYC-03 / REQ-RST-02).
        // Execution records scoped to this period CAN be restored when requested.
        if (cmd.IncludeExecutionRecords)
        {
            var softDeletedRecords = await _db.ExecutionRecords
                .IgnoreQueryFilters()
                .Where(e => e.PeriodId == cmd.PeriodId && e.DeletedAt != null)
                .ToListAsync(ct);

            foreach (var record in softDeletedRecords)
                record.Restore();
        }

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
