using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeletePeriod;

public sealed class DeletePeriodHandler : IRequestHandler<DeletePeriodCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public DeletePeriodHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(DeletePeriodCommand cmd, CancellationToken ct)
    {
        var period = await _db.Periods
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.CycleId != cmd.CycleId || period.Cycle?.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        // Soft-delete BudgetLines in this Period
        var budgetLines = await _db.BudgetLines
            .IgnoreQueryFilters()
            .Where(bl => bl.PeriodId == cmd.PeriodId && bl.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var line in budgetLines)
            line.SoftDelete();

        period.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(period.Id);
    }
}
