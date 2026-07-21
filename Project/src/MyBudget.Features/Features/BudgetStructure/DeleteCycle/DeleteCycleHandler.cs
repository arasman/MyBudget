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

        // Load non-deleted periods
        var periods = await _db.Periods
            .IgnoreQueryFilters()
            .Where(p => p.CycleId == cmd.CycleId && p.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var period in periods)
            period.SoftDelete();

        // REQ-CYC-03: BudgetLines are Budget-scoped (no PeriodId FK); they are NOT cascade-deleted here.

        cycle.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
