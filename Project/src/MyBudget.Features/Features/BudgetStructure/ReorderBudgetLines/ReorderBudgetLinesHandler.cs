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
        // Verify Period belongs to the budget via Cycle
        var periodExists = await _db.Periods
            .Include(p => p.Cycle)
            .AnyAsync(p => p.Id == cmd.PeriodId && p.Cycle!.BudgetId == cmd.BudgetId, ct);

        if (!periodExists)
            return Result<bool>.Failure("PERIOD_NOT_FOUND");

        // Load all active BudgetLines for this Period
        var allLines = await _db.BudgetLines
            .Where(l => l.PeriodId == cmd.PeriodId)
            .ToListAsync(ct);

        // Validate: all provided IDs belong to this Period
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
