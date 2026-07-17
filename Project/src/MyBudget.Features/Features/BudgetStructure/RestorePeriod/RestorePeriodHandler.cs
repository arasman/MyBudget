using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestorePeriod;

public sealed class RestorePeriodHandler : IRequestHandler<RestorePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestorePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestorePeriodCommand cmd, CancellationToken ct)
    {
        // Parent guard: Cycle must exist and not be soft-deleted
        var cycle = await _db.Cycles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.Id == cmd.CycleId && c.BudgetId == cmd.BudgetId,
                ct);

        if (cycle is null)
            return Result<Guid>.Failure("CYCLE_NOT_FOUND");

        if (cycle.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        // Load soft-deleted Period
        var period = await _db.Periods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.Id == cmd.PeriodId && p.CycleId == cmd.CycleId && p.DeletedAt != null,
                ct);

        if (period is null)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        period.Restore();

        // Cascade restore soft-deleted BudgetLines for this Period
        var budgetLines = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Where(bl => bl.PeriodId == cmd.PeriodId && bl.DeletedAt != null)
            .ToListAsync(ct);

        var restoredLineIds = new List<Guid>();
        foreach (var line in budgetLines)
        {
            line.Restore();
            restoredLineIds.Add(line.Id);
        }

        // Optionally restore child ExecutionRecords
        if (cmd.IncludeExecutionRecords && restoredLineIds.Count > 0)
        {
            var executionRecords = await _db.ExecutionRecords
                .IgnoreQueryFilters()
                .Where(e => restoredLineIds.Contains(e.BudgetLineId) && e.DeletedAt != null)
                .ToListAsync(ct);

            foreach (var record in executionRecords)
                record.Restore();
        }

        // EF Core wraps all tracked changes in a single implicit transaction — no explicit BeginTransactionAsync needed.
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
