using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreatePeriod;

public sealed class CreatePeriodHandler : IRequestHandler<CreatePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreatePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreatePeriodCommand cmd, CancellationToken ct)
    {
        // Load parent Cycle and verify BudgetId (resource isolation — ADR-BS-08)
        var cycle = await _db.Cycles
            .FirstOrDefaultAsync(c => c.Id == cmd.CycleId, ct);

        if (cycle is null || cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        // Period dates must fall within Cycle range
        if (cmd.StartDate < cycle.StartDate || cmd.EndDate > cycle.EndDate)
            return Result<Guid>.Failure("PERIOD_OUT_OF_CYCLE_RANGE");

        // Overlap check within same Cycle
        var hasOverlap = await _db.Periods.AnyAsync(p =>
            p.CycleId   == cmd.CycleId &&
            p.StartDate <  cmd.EndDate  &&
            p.EndDate   >  cmd.StartDate, ct);

        if (hasOverlap)
            return Result<Guid>.Failure("PERIOD_DATE_OVERLAP");

        var period = Period.Create(cycle.BudgetId, cmd.CycleId, cmd.Name, cmd.PeriodNumber, cmd.StartDate, cmd.EndDate);
        _db.Periods.Add(period);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
