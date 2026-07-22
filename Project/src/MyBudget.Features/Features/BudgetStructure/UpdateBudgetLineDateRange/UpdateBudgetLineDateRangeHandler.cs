using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineDateRange;

/// <summary>
/// REQ-BL-DATERANGE-1: Updates the date range of a BudgetLine.
/// Guards:
///   - RANGE_WOULD_ORPHAN_REVISION (422): domain method throws when a revision falls outside.
///   - RANGE_WOULD_ORPHAN_EXECUTION (409): active execution records exist outside the new range.
/// Audit: SaveChangesAsync interceptor captures the BudgetLine "Updated" event automatically
///        (BudgetLine is a tracked IAuditableEntity modified via EF).
/// </summary>
public sealed class UpdateBudgetLineDateRangeHandler
    : IRequestHandler<UpdateBudgetLineDateRangeCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateBudgetLineDateRangeHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(
        UpdateBudgetLineDateRangeCommand cmd, CancellationToken ct)
    {
        var line = await _db.BudgetLines
            .Include(l => l.Revisions)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-BL-DATERANGE-1: RANGE_WOULD_ORPHAN_EXECUTION (409)
        // Check for active execution records that fall outside the new date range
        var hasOrphanedExecutions = await _db.ExecutionRecords
            .AnyAsync(e =>
                e.BudgetLineId == cmd.LineId
                && e.DeletedAt  == null
                && (e.OperationDate < cmd.StartDate
                    || (cmd.EndDate.HasValue && e.OperationDate > cmd.EndDate.Value)),
                ct);

        if (hasOrphanedExecutions)
            return Result<Guid>.Failure("RANGE_WOULD_ORPHAN_EXECUTION");

        // When there is exactly one revision and its ValidFrom matches the line's current StartDate,
        // sync it with the new StartDate so UpdateDateRange does not reject it as orphaned.
        if (line.Revisions.Count == 1)
        {
            var original = line.Revisions.First();
            if (original.ValidFrom == line.StartDate && original.ValidFrom != cmd.StartDate)
            {
                original.SyncValidFrom(cmd.StartDate);
            }
        }

        // Domain method — may throw RANGE_WOULD_ORPHAN_REVISION
        try
        {
            line.UpdateDateRange(cmd.StartDate, cmd.EndDate);
        }
        catch (InvalidOperationException ex) when (ex.Message == "RANGE_WOULD_ORPHAN_REVISION")
        {
            return Result<Guid>.Failure("RANGE_WOULD_ORPHAN_REVISION");
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Failure("DATE_RANGE_CONCURRENCY_CONFLICT");
        }

        return Result<Guid>.Success(line.Id);
    }
}
