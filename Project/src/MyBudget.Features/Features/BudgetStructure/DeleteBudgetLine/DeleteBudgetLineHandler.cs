using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;

public sealed class DeleteBudgetLineHandler : IRequestHandler<DeleteBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeleteBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeleteBudgetLineCommand cmd, CancellationToken ct)
    {
        // Stub: load BudgetLine by BudgetId + LineId (no PeriodId — PR2a scope for full guard)
        var line = await _db.BudgetLines
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // TODO PR2a: IsClosed guard removed (REQ-BL-04 — closed period no longer blocks delete)

        // REQ-EXEC-CASCADE-1: cascade soft-delete to all non-deleted child ExecutionRecords
        line.SoftDelete();

        var executionRecords = await _db.ExecutionRecords
            .IgnoreQueryFilters()
            .Where(e => e.BudgetLineId == cmd.LineId && e.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var record in executionRecords)
            record.SoftDelete();

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
