using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdatePeriod;

public sealed class UpdatePeriodHandler : IRequestHandler<UpdatePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdatePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdatePeriodCommand cmd, CancellationToken ct)
    {
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.CycleId != cmd.CycleId || period.Cycle?.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        var cycle = period.Cycle!;

        // Period dates must fall within Cycle range
        if (cmd.StartDate < cycle.StartDate || cmd.EndDate > cycle.EndDate)
            return Result<Guid>.Failure("PERIOD_OUT_OF_CYCLE_RANGE");

        // Overlap check excluding self
        var hasOverlap = await _db.Periods.AnyAsync(p =>
            p.CycleId   == cmd.CycleId &&
            p.Id        != cmd.PeriodId &&
            p.StartDate <  cmd.EndDate  &&
            p.EndDate   >  cmd.StartDate, ct);

        if (hasOverlap)
            return Result<Guid>.Failure("PERIOD_DATE_OVERLAP");

        period.Update(cmd.Name, cmd.PeriodNumber, cmd.StartDate, cmd.EndDate);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
