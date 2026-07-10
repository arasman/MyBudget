using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

public sealed class UpdateBudgetLineHandler : IRequestHandler<UpdateBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public UpdateBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(UpdateBudgetLineCommand cmd, CancellationToken ct)
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

        // Update line fields
        line.Update(cmd.CategoryGroupId, cmd.CategoryId, cmd.Name, cmd.LineType, cmd.IsRecurring);

        // ADR-BS-06: Insert NEW BudgetLineRevision — never modify existing ones
        var revision = BudgetLineRevision.Create(line.Id, cmd.BudgetedAmount, cmd.Currency);
        _db.BudgetLineRevisions.Add(revision);

        await _db.SaveChangesAsync(ct);
        return Result<Guid>.Success(line.Id);
    }
}
