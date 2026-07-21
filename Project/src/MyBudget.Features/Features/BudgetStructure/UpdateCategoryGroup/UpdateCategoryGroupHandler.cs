using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategoryGroup;

public sealed class UpdateCategoryGroupHandler : IRequestHandler<UpdateCategoryGroupCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateCategoryGroupHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateCategoryGroupCommand cmd, CancellationToken ct)
    {
        var group = await _db.CategoryGroups
            .FirstOrDefaultAsync(g => g.Id == cmd.GroupId, ct);

        if (group is null || group.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("CATEGORY_GROUP_NOT_FOUND");

        // Uniqueness check excluding self — includes soft-deleted rows (REQ-CG-02)
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var isDuplicate = await _db.CategoryGroups.IgnoreQueryFilters().AnyAsync(g =>
            g.BudgetId == cmd.BudgetId &&
            g.Id       != cmd.GroupId  &&
            g.Name.ToLower() == normalizedName, ct);

        if (isDuplicate)
            return Result<Guid>.Failure("CATEGORY_GROUP_NAME_DUPLICATE");

        group.Update(cmd.Name, cmd.DisplayOrder);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(group.Id);
    }
}
