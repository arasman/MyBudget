using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategories;

public sealed class ReorderCategoriesHandler : IRequestHandler<ReorderCategoriesCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public ReorderCategoriesHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(ReorderCategoriesCommand cmd, CancellationToken ct)
    {
        // Verify CategoryGroup belongs to the budget
        var group = await _db.CategoryGroups
            .FirstOrDefaultAsync(g => g.Id == cmd.CategoryGroupId, ct);

        if (group is null || group.BudgetId != cmd.BudgetId)
            return Result<bool>.Failure("CATEGORY_GROUP_NOT_FOUND");

        var allCategories = await _db.Categories
            .Where(c => c.CategoryGroupId == cmd.CategoryGroupId)
            .ToListAsync(ct);

        if (cmd.OrderedIds.Count != allCategories.Count)
            return Result<bool>.Failure("REORDER_LIST_INCOMPLETE");

        var distinctIds = cmd.OrderedIds.Distinct().ToList();
        if (distinctIds.Count != cmd.OrderedIds.Count)
            return Result<bool>.Failure("REORDER_LIST_INVALID");

        var allCategoryIds = allCategories.Select(c => c.Id).ToHashSet();
        if (!cmd.OrderedIds.All(id => allCategoryIds.Contains(id)))
            return Result<bool>.Failure("REORDER_LIST_INVALID");

        for (var i = 0; i < cmd.OrderedIds.Count; i++)
        {
            var category = allCategories.First(c => c.Id == cmd.OrderedIds[i]);
            category.SetDisplayOrder(i + 1);
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
