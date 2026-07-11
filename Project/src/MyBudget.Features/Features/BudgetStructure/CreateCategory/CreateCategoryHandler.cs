using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategory;

public sealed class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateCategoryHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateCategoryCommand cmd, CancellationToken ct)
    {
        // Verify CategoryGroup belongs to the budget
        var group = await _db.CategoryGroups
            .FirstOrDefaultAsync(g => g.Id == cmd.CategoryGroupId, ct);

        if (group is null || group.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CATEGORY_GROUP_NOT_FOUND");

        // Case-insensitive unique name within group
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var isDuplicate = await _db.Categories.AnyAsync(c =>
            c.CategoryGroupId == cmd.CategoryGroupId &&
            c.Name.ToLower() == normalizedName, ct);

        if (isDuplicate)
            return Result<Guid>.Failure("CATEGORY_NAME_DUPLICATE");

        var category = Category.Create(group.BudgetId, cmd.CategoryGroupId, cmd.Name, cmd.DisplayOrder);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(category.Id);
    }
}
