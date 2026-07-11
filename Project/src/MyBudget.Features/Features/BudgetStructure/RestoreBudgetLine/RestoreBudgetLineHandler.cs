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
        // Parent guard: load Period (with IgnoreQueryFilters to see soft-deleted) and verify BudgetId via Cycle
        var period = await _db.Periods
            .IgnoreQueryFilters()
            .Include(p => p.Cycle)
            .FirstOrDefaultAsync(p => p.Id == cmd.PeriodId, ct);

        if (period is null || period.Cycle is null || period.Cycle.BudgetId != cmd.BudgetId)
            return Result<Guid>.Failure("PERIOD_NOT_FOUND");

        if (period.DeletedAt != null)
            return Result<Guid>.Failure("PARENT_IS_DELETED");

        // Load soft-deleted BudgetLine
        var line = await _db.BudgetLines
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                bl => bl.Id == cmd.BudgetLineId && bl.PeriodId == cmd.PeriodId && bl.DeletedAt != null,
                ct);

        if (line is null)
            return Result<Guid>.Failure("BUDGET_LINE_NOT_FOUND");

        line.Restore();
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
