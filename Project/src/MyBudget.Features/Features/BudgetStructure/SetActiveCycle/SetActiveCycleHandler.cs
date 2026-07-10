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

        // Use explicit transaction to enforce deactivate-then-activate order.
        // The unique partial index IX_Cycles_BudgetId_IsActive prevents two active cycles;
        // EF Core may batch the two UPDATEs in any order, so we split into two saves.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Step 1: deactivate the current active cycle (if any, and not the target)
        var currentActive = await _db.Cycles
            .FirstOrDefaultAsync(c => c.BudgetId == cmd.BudgetId && c.IsActive, ct);

        if (currentActive is not null && currentActive.Id != cmd.CycleId)
        {
            currentActive.Deactivate();
            await _db.SaveChangesAsync(ct);
        }

        // Step 2: activate target
        target.Activate();
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return Result<Guid>.Success(target.Id);
    }
}
