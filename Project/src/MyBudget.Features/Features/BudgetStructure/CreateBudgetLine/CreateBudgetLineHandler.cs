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

        // Name uniqueness per (PeriodId, CategoryGroupId, CategoryId) — includes soft-deleted lines (REQ-BL-NAME-1)
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var isDuplicateName = await _db.BudgetLines.IgnoreQueryFilters().AnyAsync(l =>
            l.PeriodId        == cmd.PeriodId        &&
            l.CategoryGroupId == cmd.CategoryGroupId &&
            l.CategoryId      == cmd.CategoryId      &&
            l.Name.ToLower() == normalizedName, ct);

        if (isDuplicateName)
            return Result<Guid>.Failure("BUDGET_LINE_NAME_DUPLICATE");

        // Resolve CurrencyId: explicit or fall back to Cycle.DefaultCurrencyId
        var currencyId = cmd.CurrencyId ?? period.Cycle.DefaultCurrencyId;

        // Determine DisplayOrder: count of existing active lines in same (PeriodId, CategoryGroupId, CategoryId) + 1
        var existingCount = await _db.BudgetLines
            .CountAsync(l => l.PeriodId == cmd.PeriodId
                          && l.CategoryGroupId == cmd.CategoryGroupId
                          && l.CategoryId == cmd.CategoryId, ct);

        // ADR-BS-06: Create BudgetLine + initial BudgetLineRevision
        var budgetId = period.Cycle!.BudgetId;
        var line = BudgetLine.Create(
            budgetId,
            cmd.PeriodId,
            cmd.CategoryGroupId,
            cmd.CategoryId,
            cmd.Name,
            cmd.LineType,
            cmd.IsRecurring,
            existingCount + 1);

        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync(ct); // persist to get the Id

        var revision = BudgetLineRevision.Create(budgetId, line.Id, cmd.BudgetedAmount, currencyId);
        _db.BudgetLineRevisions.Add(revision);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
