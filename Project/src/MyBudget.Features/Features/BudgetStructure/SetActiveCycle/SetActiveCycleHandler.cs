using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.SetActiveCycle;

public sealed class SetActiveCycleHandler : IRequestHandler<SetActiveCycleCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public SetActiveCycleHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(SetActiveCycleCommand cmd, CancellationToken ct)
    {
        // Load target cycle (verify belongs to budget and is not deleted)
        var target = await _db.Cycles
            .FirstOrDefaultAsync(c => c.Id == cmd.CycleId, ct);

        if (target is null || target.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        // Load currently active cycle (if any)
        var currentActive = await _db.Cycles
            .FirstOrDefaultAsync(c => c.BudgetId == cmd.BudgetId && c.IsActive, ct);

        if (currentActive is not null && currentActive.Id != cmd.CycleId)
            currentActive.Deactivate();

        target.Activate();

        // Single SaveChangesAsync = single transaction (ADR-BS-03)
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(target.Id);
    }
}
