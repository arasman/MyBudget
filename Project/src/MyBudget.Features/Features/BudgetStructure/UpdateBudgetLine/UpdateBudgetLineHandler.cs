using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

// TODO PR2a: full handler rewrite — metadata update + SplitRevision for amount change
public sealed class UpdateBudgetLineHandler : IRequestHandler<UpdateBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateBudgetLineCommand cmd, CancellationToken ct)
    {
        // Stub: load BudgetLine by BudgetId + LineId (no PeriodId)
        var line = await _db.BudgetLines
            .FirstOrDefaultAsync(l => l.Id == cmd.LineId && l.BudgetId == cmd.BudgetId, ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        // Metadata update (stub — full logic in PR2a)
        line.Update(cmd.CategoryGroupId, cmd.CategoryId, cmd.Name, cmd.LineType);

        // TODO PR2a: if cmd.ValidFrom.HasValue && cmd.BudgetedAmount.HasValue -> call line.SplitRevision(...)

        await _db.SaveChangesAsync(ct);
        return Result<Guid>.Success(line.Id);
    }
}
