using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

// TODO PR2a: full handler rewrite — budget-scoped, StartDate/EndDate, initial revision via BudgetLine.Create()
public sealed class CreateBudgetLineHandler : IRequestHandler<CreateBudgetLineCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateBudgetLineHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateBudgetLineCommand cmd, CancellationToken ct)
    {
        // Stub: verify budget exists
        var budgetExists = await _db.Budgets
            .AnyAsync(b => b.Id == cmd.BudgetId, ct);

        if (!budgetExists)
            return Result<Guid>.Failure("BUDGET_NOT_FOUND");

        // Stub placeholders — full logic in PR2a
        var startDate     = cmd.StartDate;
        var endDate       = cmd.EndDate;
        var initialAmount = cmd.InitialAmount;
        var currencyId    = cmd.CurrencyId ?? Guid.Empty;

        var line = BudgetLine.Create(
            cmd.BudgetId,
            cmd.CategoryGroupId,
            cmd.CategoryId,
            cmd.Name,
            cmd.LineType,
            startDate,
            endDate,
            initialAmount,
            currencyId);

        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
