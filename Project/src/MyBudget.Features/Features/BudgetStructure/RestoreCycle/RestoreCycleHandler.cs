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
        // Load soft-deleted Cycle (IncludeExecutionRecords is a no-op forward-compat param)
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

        var restoredPeriodIds = new List<Guid>();
        foreach (var period in periods)
        {
            period.Restore();
            restoredPeriodIds.Add(period.Id);
        }

        // Load soft-deleted BudgetLines for restored Periods only
        if (restoredPeriodIds.Count > 0)
        {
            var budgetLines = await _db.BudgetLines
                .IgnoreQueryFilters()
                .Where(bl => restoredPeriodIds.Contains(bl.PeriodId) && bl.DeletedAt != null)
                .ToListAsync(ct);

            foreach (var line in budgetLines)
                line.Restore();
        }

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
