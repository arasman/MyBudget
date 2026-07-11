using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCycle;

public sealed class CreateCycleHandler : IRequestHandler<CreateCycleCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateCycleHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateCycleCommand cmd, CancellationToken ct)
    {
        // Check for overlapping non-deleted cycles in the same budget
        var hasOverlap = await _db.Cycles.AnyAsync(c =>
            c.BudgetId == cmd.BudgetId &&
            c.StartDate < cmd.EndDate &&
            c.EndDate   > cmd.StartDate, ct);

        if (hasOverlap)
            return Result<Guid>.Failure("CYCLE_DATE_OVERLAP");

        var cycle = Cycle.Create(
            cmd.BudgetId, cmd.Name, cmd.StartDate, cmd.EndDate,
            cmd.DefaultCurrencyId, cmd.AlternateCurrencyId, cmd.ExchangeRate);
        _db.Cycles.Add(cycle);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(cycle.Id);
    }
}
