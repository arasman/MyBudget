using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCycle;

public sealed class UpdateCycleHandler : IRequestHandler<UpdateCycleCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateCycleHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateCycleCommand cmd, CancellationToken ct)
    {
        var cycle = await _db.Cycles
            .Include(c => c.Periods)
            .FirstOrDefaultAsync(c => c.Id == cmd.CycleId, ct);

        if (cycle is null || cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        // Overlap check excluding self
        var hasOverlap = await _db.Cycles.AnyAsync(c =>
            c.BudgetId  == cmd.BudgetId &&
            c.Id        != cmd.CycleId  &&
            c.StartDate <  cmd.EndDate  &&
            c.EndDate   >  cmd.StartDate, ct);

        if (hasOverlap)
            return Result<Guid>.Failure("CYCLE_DATE_OVERLAP");

        // Verify no Period falls outside the new date range
        var periodOutOfRange = cycle.Periods.Any(p =>
            p.StartDate < cmd.StartDate || p.EndDate > cmd.EndDate);

        if (periodOutOfRange)
            return Result<Guid>.Failure("CYCLE_PERIOD_OUT_OF_RANGE");

        cycle.Update(cmd.Name, cmd.StartDate, cmd.EndDate);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
