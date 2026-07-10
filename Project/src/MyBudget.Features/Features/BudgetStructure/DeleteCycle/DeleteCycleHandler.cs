using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCycle;

public sealed class DeleteCycleHandler : IRequestHandler<DeleteCycleCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteCycleHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteCycleCommand cmd, CancellationToken ct)
    {
        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(c => c.Id == cmd.CycleId, ct);

        if (cycle is null || cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        // Load non-deleted periods (bypassing global query filter for explicit control)
        var periods = await _db.Periods
            .IgnoreQueryFilters()
            .Where(p => p.CycleId == cmd.CycleId && p.DeletedAt == null)
            .ToListAsync(ct);

        var periodIds = periods.Select(p => p.Id).ToList();

        if (periodIds.Count > 0)
        {
            // Soft-delete BudgetLines within those periods
            var budgetLines = await _db.BudgetLines
                .IgnoreQueryFilters()
                .Where(bl => periodIds.Contains(bl.PeriodId) && bl.DeletedAt == null)
                .ToListAsync(ct);

            var now = DateTimeOffset.UtcNow;
            foreach (var line in budgetLines)
                line.SoftDelete();

            foreach (var period in periods)
                period.SoftDelete();
        }

        cycle.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
