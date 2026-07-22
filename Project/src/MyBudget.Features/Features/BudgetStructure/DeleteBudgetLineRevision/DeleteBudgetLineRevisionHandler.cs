using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;
using Entities = MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLineRevision;

/// <summary>
/// REQ-BLR-03: Deletes a BudgetLineRevision and repairs the gapless chain.
/// Order of operations:
///   1. Load BudgetLine + Revisions
///   2. Query active (non-soft-deleted) executions for the revision window
///   3. Call line.DeleteRevision(revisionId, hasActiveExecutions)
///   4. Explicitly Remove the entity from BudgetLineRevisions DbSet
///   5. Write explicit AuditLog (BEFORE SaveChangesAsync — interceptor may miss physical deletes)
///   6. SaveChangesAsync
/// </summary>
public sealed class DeleteBudgetLineRevisionHandler
    : IRequestHandler<DeleteBudgetLineRevisionCommand, Result<Guid>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteBudgetLineRevisionHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<Guid>> Handle(
        DeleteBudgetLineRevisionCommand cmd, CancellationToken ct)
    {
        var line = await _db.BudgetLines
            .Include(l => l.Revisions)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        var revision = line.Revisions.FirstOrDefault(r => r.Id == cmd.RevisionId);
        if (revision is null)
            return Result<Guid>.Failure("REVISION_NOT_FOUND");

        // REQ-BLR-03: Check active executions in the revision's validity window
        // (OperationDate falls within [ValidFrom, ValidTo])
        var hasActiveExecutions = await _db.ExecutionRecords
            .AnyAsync(e =>
                e.BudgetLineId == cmd.LineId
                && e.DeletedAt  == null
                && e.OperationDate >= revision.ValidFrom
                && (revision.ValidTo == null || e.OperationDate <= revision.ValidTo),
                ct);

        // Domain method — may throw CANNOT_DELETE_ORIGINAL_REVISION or REVISION_HAS_ACTIVE_EXECUTIONS
        Entities.BudgetLineRevision removed;
        try
        {
            removed = line.DeleteRevision(cmd.RevisionId, hasActiveExecutions);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message switch
            {
                "CANNOT_DELETE_ORIGINAL_REVISION"  => Result<Guid>.Failure("CANNOT_DELETE_ORIGINAL_REVISION"),
                "REVISION_HAS_ACTIVE_EXECUTIONS"   => Result<Guid>.Failure("REVISION_HAS_ACTIVE_EXECUTIONS"),
                _                                  => Result<Guid>.Failure(ex.Message),
            };
        }

        // Explicit remove — nav-collection Remove alone does NOT mark EntityState.Deleted
        _db.BudgetLineRevisions.Remove(removed);

        // Explicit audit entry BEFORE SaveChangesAsync — interceptor does not capture physical deletes
        var audit = Entities.AuditLog.Create(
            entityName: "BudgetLineRevision",
            entityId:   removed.Id,
            action:     "BudgetLineRevisionDeleted",
            userId:     _currentUser.UserId,
            beforeJson: null,
            afterJson:  null,
            budgetId:   cmd.BudgetId);

        _db.AuditLogs.Add(audit);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Failure("REVISION_CONCURRENCY_CONFLICT");
        }

        return Result<Guid>.Success(removed.Id);
    }
}
