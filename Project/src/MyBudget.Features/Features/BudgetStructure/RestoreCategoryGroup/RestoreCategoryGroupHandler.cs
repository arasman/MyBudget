using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategoryGroup;

public sealed class RestoreCategoryGroupHandler : IRequestHandler<RestoreCategoryGroupCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreCategoryGroupHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreCategoryGroupCommand cmd, CancellationToken ct)
    {
        // Load soft-deleted CategoryGroup (Budget is not soft-deletable — no parent guard)
        var group = await _db.CategoryGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                g => g.Id == cmd.CategoryGroupId && g.BudgetId == cmd.BudgetId && g.DeletedAt != null,
                ct);

        if (group is null)
            return Result<Guid>.Failure("CATEGORY_GROUP_NOT_FOUND");

        group.Restore();

        // Restore all soft-deleted Categories for this group
        var categories = await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.CategoryGroupId == cmd.CategoryGroupId && c.DeletedAt != null)
            .ToListAsync(ct);

        foreach (var category in categories)
            category.Restore();

        // Restore all soft-deleted BudgetLines scoped by CategoryGroupId
        var budgetLines = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Where(bl => bl.CategoryGroupId == cmd.CategoryGroupId && bl.DeletedAt != null)
            .ToListAsync(ct);

        var restoredLineIds = new List<Guid>();
        foreach (var line in budgetLines)
        {
            line.Restore();
            restoredLineIds.Add(line.Id);
        }

        // REQ-EXEC-CASCADE-2: restore child ExecutionRecords when IncludeExecutionRecords=true
        if (cmd.IncludeExecutionRecords && restoredLineIds.Count > 0)
        {
            var executionRecords = await _db.ExecutionRecords
                .IgnoreQueryFilters()
                .Where(e => restoredLineIds.Contains(e.BudgetLineId) && e.DeletedAt != null)
                .ToListAsync(ct);

            foreach (var record in executionRecords)
                record.Restore();
        }

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(group.Id);
    }
}
