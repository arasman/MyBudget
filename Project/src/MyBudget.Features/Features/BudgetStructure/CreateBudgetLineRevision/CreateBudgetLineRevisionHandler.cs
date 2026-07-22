using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLineRevision;

/// <summary>
/// REQ-BLR-02: Creates a new revision via BudgetLine.SplitRevision.
/// Guards: ValidFrom must be within BudgetLine date range.
/// Concurrency: DbUpdateConcurrencyException → REVISION_CONCURRENCY_CONFLICT (409).
/// </summary>
public sealed class CreateBudgetLineRevisionHandler
    : IRequestHandler<CreateBudgetLineRevisionCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateBudgetLineRevisionHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(
        CreateBudgetLineRevisionCommand cmd, CancellationToken ct)
    {
        var line = await _db.BudgetLines
            .Include(l => l.Revisions)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-BLR-02: ValidFrom must be within BudgetLine date range
        if (cmd.ValidFrom < line.StartDate)
            return Result<Guid>.Failure("REVISION_OUTSIDE_LINE_DATE_RANGE");

        if (line.EndDate.HasValue && cmd.ValidFrom > line.EndDate.Value)
            return Result<Guid>.Failure("REVISION_OUTSIDE_LINE_DATE_RANGE");

        // Prefer explicit currencyId; fall back to the latest revision's currency
        var currencyId = cmd.CurrencyId
            ?? line.Revisions.MaxBy(r => r.ValidFrom)?.CurrencyId
            ?? CurrencySeeds.GtqId;

        // Track existing revision IDs before the split so we can identify new ones after
        var existingRevisionIds = line.Revisions.Select(r => r.Id).ToHashSet();

        line.SplitRevision(cmd.ValidFrom, cmd.ValidTo, cmd.Amount, currencyId);

        // EF Core: explicitly mark new revision entities as Added
        var newRevisions = line.Revisions
            .Where(r => !existingRevisionIds.Contains(r.Id))
            .ToList();

        foreach (var rev in newRevisions)
            _db.Entry(rev).State = Microsoft.EntityFrameworkCore.EntityState.Added;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Failure("REVISION_CONCURRENCY_CONFLICT");
        }

        // Return ID of the "new" revision that starts at ValidFrom
        var createdRevision = line.Revisions
            .FirstOrDefault(r => r.ValidFrom == cmd.ValidFrom && !existingRevisionIds.Contains(r.Id))
            ?? line.Revisions.First(r => r.ValidFrom == cmd.ValidFrom);

        return Result<Guid>.Success(createdRevision.Id);
    }
}
