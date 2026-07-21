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
        var budgetExists = await _db.Budgets
            .AnyAsync(b => b.Id == cmd.BudgetId, ct);

        if (!budgetExists)
            return Result<Guid>.Failure("BUDGET_NOT_FOUND");

        // REQ-BL-NAME-1: name must be unique within the budget, including soft-deleted lines
        var nameExists = await _db.BudgetLines
            .IgnoreQueryFilters()
            .AnyAsync(bl => bl.BudgetId == cmd.BudgetId && bl.Name == cmd.Name.Trim(), ct);

        if (nameExists)
            return Result<Guid>.Failure("BUDGET_LINE_NAME_DUPLICATE");

        var currencyId = cmd.CurrencyId ?? Guid.Empty;

        var line = BudgetLine.Create(
            cmd.BudgetId,
            cmd.CategoryGroupId,
            cmd.CategoryId,
            cmd.Name,
            cmd.LineType,
            cmd.StartDate,
            cmd.EndDate,
            cmd.InitialAmount,
            currencyId);

        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(line.Id);
    }
}
