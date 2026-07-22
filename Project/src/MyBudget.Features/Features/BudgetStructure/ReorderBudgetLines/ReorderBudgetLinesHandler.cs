using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;

public sealed class ReorderBudgetLinesHandler : IRequestHandler<ReorderBudgetLinesCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public ReorderBudgetLinesHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(ReorderBudgetLinesCommand cmd, CancellationToken ct)
    {
        // Verify Budget exists
        var budgetExists = await _db.Budgets
            .AnyAsync(b => b.Id == cmd.BudgetId, ct);

        if (!budgetExists)
            return Result<bool>.Failure("BUDGET_NOT_FOUND");

        // Load all active BudgetLines for this Budget
        var allLines = await _db.BudgetLines
            .Where(l => l.BudgetId == cmd.BudgetId)
            .ToListAsync(ct);

        // Validate: all provided IDs belong to this Budget
        var allLineIds = allLines.Select(l => l.Id).ToHashSet();
        if (!cmd.OrderedIds.All(id => allLineIds.Contains(id)))
            return Result<bool>.Failure("REORDER_ID_NOT_IN_SCOPE");

        // Assign sequential DisplayOrder
        for (var i = 0; i < cmd.OrderedIds.Length; i++)
        {
            var line = allLines.First(l => l.Id == cmd.OrderedIds[i]);
            line.SetDisplayOrder(i + 1);
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
