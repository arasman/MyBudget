using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategory;

public sealed class RestoreCategoryHandler : IRequestHandler<RestoreCategoryCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreCategoryHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreCategoryCommand cmd, CancellationToken ct)
    {
        // Parent guard: CategoryGroup must exist and not be soft-deleted
        var group = await _db.CategoryGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                g => g.Id == cmd.CategoryGroupId && g.BudgetId == cmd.BudgetId,
                ct);

        if (group is null)
            return Result<Guid>.Failure("CATEGORY_GROUP_NOT_FOUND");

        if (group.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        // Load soft-deleted Category
        var category = await _db.Categories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.Id == cmd.CategoryId && c.CategoryGroupId == cmd.CategoryGroupId && c.DeletedAt != null,
                ct);

        if (category is null)
            return Result<Guid>.Failure("CATEGORY_NOT_FOUND");

        category.Restore();

        // Restore soft-deleted BudgetLines for this Category
        var budgetLines = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Where(bl => bl.CategoryId == cmd.CategoryId && bl.DeletedAt != null)
            .ToListAsync(ct);

        foreach (var line in budgetLines)
            line.Restore();

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(category.Id);
    }
}
