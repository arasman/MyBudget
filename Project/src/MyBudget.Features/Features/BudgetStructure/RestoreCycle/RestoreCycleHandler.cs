using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCycle;

public sealed class RestoreCycleHandler : IRequestHandler<RestoreCycleCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreCycleHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreCycleCommand cmd, CancellationToken ct)
    {
        var cycle = await _db.Cycles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.Id == cmd.CycleId && c.BudgetId == cmd.BudgetId && c.DeletedAt != null,
                ct);

        if (cycle is null)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        cycle.Restore();

        // Load soft-deleted Periods for this Cycle
        var periods = await _db.Periods
            .IgnoreQueryFilters()
            .Where(p => p.CycleId == cmd.CycleId && p.DeletedAt != null)
            .ToListAsync(ct);

        foreach (var period in periods)
            period.Restore();

        // REQ-RST-02: BudgetLines are Budget-scoped (no PeriodId FK); they are NOT cascade-restored here.

        // REQ-EXEC-CASCADE-2: ExecutionRecord restore is handled separately via RestoreBudgetLine
        if (cmd.IncludeExecutionRecords)
        {
            // No-op here — execution records tied to BudgetLines, not Periods (stub)
        }

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
