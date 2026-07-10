using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.SetPeriodStatus;

public sealed class SetPeriodStatusHandler : IRequestHandler<SetPeriodStatusCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public SetPeriodStatusHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(SetPeriodStatusCommand cmd, CancellationToken ct)
    {
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.CycleId != cmd.CycleId || period.Cycle?.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        period.SetClosed(cmd.IsClosed);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
