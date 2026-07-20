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

        // Name uniqueness per (PeriodId, CategoryGroupId, CategoryId), self-excluded — includes soft-deleted (REQ-BL-NAME-1)
        var normalizedName = cmd.Name.Trim().ToLowerInvariant();
        var categoryGroupId = cmd.CategoryGroupId;
        var categoryId      = cmd.CategoryId;
        var isDuplicateName = await _db.BudgetLines.IgnoreQueryFilters().AnyAsync(l =>
            l.PeriodId        == cmd.PeriodId    &&
            l.Id              != cmd.LineId       &&
            l.CategoryGroupId == categoryGroupId  &&
            l.CategoryId      == categoryId       &&
            l.Name.ToLower() == normalizedName, ct);

        if (isDuplicateName)
            return Result<Guid>.Failure("BUDGET_LINE_NAME_DUPLICATE");

        // Resolve CurrencyId: explicit or fall back to Cycle.DefaultCurrencyId
        var currencyId = cmd.CurrencyId ?? line.Period.Cycle.DefaultCurrencyId;

        // Update line fields
        line.Update(cmd.CategoryGroupId, cmd.CategoryId, cmd.Name, cmd.LineType, cmd.IsRecurring);

        // ADR-BS-06: Insert NEW BudgetLineRevision — never modify existing ones
        var revision = BudgetLineRevision.Create(line.Period!.Cycle!.BudgetId, line.Id, cmd.BudgetedAmount, currencyId);
        _db.BudgetLineRevisions.Add(revision);

        await _db.SaveChangesAsync(ct);
        return Result<Guid>.Success(line.Id);
    }
}
