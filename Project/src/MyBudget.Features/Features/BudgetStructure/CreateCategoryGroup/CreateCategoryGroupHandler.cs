using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;

public sealed class CreateCategoryGroupHandler : IRequestHandler<CreateCategoryGroupCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateCategoryGroupHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateCategoryGroupCommand cmd, CancellationToken ct)
    {
        // Case-insensitive unique name check per budget — includes soft-deleted rows (REQ-CG-01)
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var isDuplicate = await _db.CategoryGroups.IgnoreQueryFilters().AnyAsync(g =>
            g.BudgetId == cmd.BudgetId &&
            g.Name.ToLower() == normalizedName, ct);

        if (isDuplicate)
            return Result<Guid>.Failure("CATEGORY_GROUP_NAME_DUPLICATE");

        var group = CategoryGroup.Create(cmd.BudgetId, cmd.Name, cmd.DisplayOrder);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(group.Id);
    }
}
