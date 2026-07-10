using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;

public sealed class ReorderCategoryGroupsHandler : IRequestHandler<ReorderCategoryGroupsCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public ReorderCategoryGroupsHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(ReorderCategoryGroupsCommand cmd, CancellationToken ct)
    {
        var allGroups = await _db.CategoryGroups
            .Where(g => g.BudgetId == cmd.BudgetId)
            .ToListAsync(ct);

        if (cmd.OrderedIds.Count != allGroups.Count)
            return Result<bool>.Failure("REORDER_LIST_INCOMPLETE");

        var distinctIds = cmd.OrderedIds.Distinct().ToList();
        if (distinctIds.Count != cmd.OrderedIds.Count)
            return Result<bool>.Failure("REORDER_LIST_INVALID");

        var allGroupIds = allGroups.Select(g => g.Id).ToHashSet();
        if (!cmd.OrderedIds.All(id => allGroupIds.Contains(id)))
            return Result<bool>.Failure("REORDER_LIST_INVALID");

        for (var i = 0; i < cmd.OrderedIds.Count; i++)
        {
            var group = allGroups.First(g => g.Id == cmd.OrderedIds[i]);
            group.SetDisplayOrder(i + 1);
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
