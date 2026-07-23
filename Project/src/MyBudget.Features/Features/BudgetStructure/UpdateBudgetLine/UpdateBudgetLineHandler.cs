using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

public sealed class UpdateBudgetLineHandler : IRequestHandler<UpdateBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateBudgetLineCommand cmd, CancellationToken ct)
    {
        var line = await _db.BudgetLines
            .Include(bl => bl.Revisions)
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // REQ-BL-NAME-1: name uniqueness — self-exclusion applies
        var nameConflict = await _db.BudgetLines
            .IgnoreQueryFilters()
            .AnyAsync(bl => bl.Id != cmd.LineId
                         && bl.BudgetId == cmd.BudgetId
                         && bl.Name == cmd.Name.Trim(), ct);

        if (nameConflict)
            return Result<Guid>.Failure("BUDGET_LINE_NAME_DUPLICATE");

        // Metadata update (name, category, lineType, description) — always applied
        line.Update(cmd.CategoryGroupId, cmd.CategoryId, cmd.Name, cmd.LineType, cmd.Description);

        // REQ-BL-03: Revision split — only when ValidFrom + BudgetedAmount are provided
        if (cmd.ValidFrom.HasValue && cmd.BudgetedAmount.HasValue)
        {
            // Edge Case A — IsClosed guard: if any period covers ValidFrom and is closed → reject
            var isClosed = await _db.Periods
                .AnyAsync(p => p.BudgetId == cmd.BudgetId
                            && p.StartDate <= cmd.ValidFrom.Value
                            && (p.EndDate >= cmd.ValidFrom.Value)
                            && p.IsClosed, ct);

            if (isClosed)
                return Result<Guid>.Failure("PERIOD_CLOSED");

            // Prefer explicit currencyId; fall back to the line's existing revision currency
            var currencyId = cmd.CurrencyId
                ?? line.Revisions.MaxBy(r => r.ValidFrom)?.CurrencyId
                ?? CurrencySeeds.GtqId;

            // Track existing revision IDs before the split so we can identify new ones after.
            var existingRevisionIds = line.Revisions.Select(r => r.Id).ToHashSet();

            line.SplitRevision(cmd.ValidFrom.Value, cmd.ValidTo, cmd.BudgetedAmount.Value, currencyId);

            // EF Core incorrectly tracks new entities added to a tracked collection navigation
            // as Modified (not Added) when they have a client-set GUID as PK. Fix by explicitly
            // detaching new revisions and re-adding them via the DbSet.
            var newRevisions = line.Revisions
                .Where(r => !existingRevisionIds.Contains(r.Id))
                .ToList();
            foreach (var newRev in newRevisions)
            {
                _db.Entry(newRev).State = Microsoft.EntityFrameworkCore.EntityState.Added;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<Guid>.Success(line.Id);
    }
}
