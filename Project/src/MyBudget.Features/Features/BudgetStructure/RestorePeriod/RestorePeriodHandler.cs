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

        // TODO PR2a: BudgetLines are now Budget-scoped — no cascade restore via PeriodId.
        // REQ-RESTORE-PERIOD-1: Period restore MUST NOT cascade-restore BudgetLines.

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
