using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategory;

public sealed class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteCategoryHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteCategoryCommand cmd, CancellationToken ct)
    {
        var category = await _db.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == cmd.CategoryId, ct);

        if (category is null ||
            category.CategoryGroup is null ||
            category.CategoryGroup.BudgetId != cmd.BudgetId ||
            category.CategoryGroupId != cmd.CategoryGroupId)
            return Result<Guid>.Failure("CATEGORY_NOT_FOUND");

        // Soft-delete the category only — BudgetLines retain the reference (PR4 read layer handles categoryDeleted flag)
        category.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(category.Id);
    }
}
