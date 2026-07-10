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
        // Load BudgetLine -> Period -> Cycle -> verify BudgetId
        var line = await _db.BudgetLines
            .Include(l => l.Period)
                .ThenInclude(p => p!.Cycle)
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.PeriodId == cmd.PeriodId, ct);

        if (line is null || line.Period is null || line.Period.Cycle is null ||
            line.Period.Cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // ADR-BS-05: IsClosed guard -> HTTP 409
        if (line.Period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // ADR-BS-01: BudgetLineRevision has NO soft delete (immutable, append-only)
        // Only soft-delete the BudgetLine itself
        line.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
