using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategory;

public sealed class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateCategoryHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateCategoryCommand cmd, CancellationToken ct)
    {
        var category = await _db.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == cmd.CategoryId, ct);

        if (category is null ||
            category.CategoryGroup is null ||
            category.CategoryGroup.BudgetId != cmd.BudgetId ||
            category.CategoryGroupId != cmd.CategoryGroupId)
            return Result<Guid>.Failure("CATEGORY_NOT_FOUND");

        // Uniqueness check excluding self — includes soft-deleted rows (REQ-CAT-02)
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var isDuplicate = await _db.Categories.IgnoreQueryFilters().AnyAsync(c =>
            c.CategoryGroupId == cmd.CategoryGroupId &&
            c.Id       != cmd.CategoryId            &&
            c.Name.ToLower() == normalizedName, ct);

        if (isDuplicate)
            return Result<Guid>.Failure("CATEGORY_NAME_DUPLICATE");

        category.Update(cmd.Name, cmd.DisplayOrder);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(category.Id);
    }
}
