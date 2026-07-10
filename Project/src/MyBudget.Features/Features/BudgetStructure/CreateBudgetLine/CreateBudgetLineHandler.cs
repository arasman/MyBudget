using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public sealed class CreateBudgetLineHandler : IRequestHandler<CreateBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateBudgetLineCommand cmd, CancellationToken ct)
    {
        // Load Period -> Cycle -> verify BudgetId
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.Cycle is null || period.Cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        // ADR-BS-05: IsClosed guard -> HTTP 409
        if (period.IsClosed)
            return Result<Guid>.Failure("PERIOD_CLOSED");

        // ADR-BS-06: Create BudgetLine + initial BudgetLineRevision
        var line = BudgetLine.Create(
            cmd.PeriodId,
            cmd.CategoryGroupId,
            cmd.CategoryId,
            cmd.Name,
            cmd.LineType,
            cmd.IsRecurring);

        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync(ct); // persist to get the Id

        var revision = BudgetLineRevision.Create(line.Id, cmd.BudgetedAmount, cmd.Currency);
        _db.BudgetLineRevisions.Add(revision);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
