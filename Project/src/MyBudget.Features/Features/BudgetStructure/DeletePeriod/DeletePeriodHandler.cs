using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeletePeriod;

public sealed class DeletePeriodHandler : IRequestHandler<DeletePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeletePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeletePeriodCommand cmd, CancellationToken ct)
    {
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.CycleId != cmd.CycleId || period.Cycle?.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        // REQ-CYC-03: BudgetLines are Budget-scoped (no PeriodId FK); they are NOT cascade-deleted here.

        period.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
