using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;

public sealed class RestoreBudgetLineHandler : IRequestHandler<RestoreBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreBudgetLineCommand cmd, CancellationToken ct)
    {
        // Load soft-deleted BudgetLine by BudgetId (no PeriodId — REQ-RST-05)
        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Include(bl => bl.Category)
            .Include(bl => bl.CategoryGroup)
            .FirstOrDefaultAsync(
                bl => bl.Id == cmd.BudgetLineId && bl.BudgetId == cmd.BudgetId && bl.DeletedAt != null,
                ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // Parent guards: cannot restore a line whose category or group is still deleted
        if (line.Category?.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        if (line.CategoryGroup?.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        line.Restore();

        // REQ-EXEC-CASCADE-2: restore child ExecutionRecords when IncludeExecutionRecords=true
        if (cmd.IncludeExecutionRecords)
        {
            var executionRecords = await _db.ExecutionRecords
                .IgnoreQueryFilters()
                .Where(e => e.BudgetLineId == cmd.BudgetLineId && e.DeletedAt != null)
                .ToListAsync(ct);

            foreach (var record in executionRecords)
                record.Restore();
        }

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
