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

        foreach (var line in budgetLines)
            line.Restore();

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(group.Id);
    }
}
