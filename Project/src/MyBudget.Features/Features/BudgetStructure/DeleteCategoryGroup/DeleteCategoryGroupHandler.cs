using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategoryGroup;

public sealed class DeleteCategoryGroupHandler : IRequestHandler<DeleteCategoryGroupCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteCategoryGroupHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteCategoryGroupCommand cmd, CancellationToken ct)
    {
        var group = await _db.CategoryGroups
            .FirstOrDefaultAsync(g => g.Id == cmd.GroupId, ct);

        if (group is null || group.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CATEGORY_GROUP_NOT_FOUND");

        // Soft-delete all non-deleted child Categories
        var categories = await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.CategoryGroupId == cmd.GroupId && c.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var category in categories)
            category.SoftDelete();

        // Cascade: soft-delete all non-deleted BudgetLines in this group
        var budgetLines = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Where(bl => bl.CategoryGroupId == cmd.GroupId && bl.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var line in budgetLines)
            line.SoftDelete();

        group.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(group.Id);
    }
}
