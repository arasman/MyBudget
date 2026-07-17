using Mediator;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.CreateBudget;

public sealed class CreateBudgetHandler
    : IRequestHandler<CreateBudgetCommand, Result<CreateBudgetResponse>>
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateBudgetHandler> _logger;

    public CreateBudgetHandler(AppDbContext db, ILogger<CreateBudgetHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async ValueTask<Result<CreateBudgetResponse>> Handle(
        CreateBudgetCommand cmd, CancellationToken ct)
    {
        var budget = Budget.Create(cmd.Name, cmd.UserId);
        _db.Budgets.Add(budget);

        var membership = BudgetMembership.Create(budget.Id, cmd.UserId, BudgetRole.Owner);
        _db.BudgetMemberships.Add(membership);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Budget created: {BudgetId} by user {UserId}", budget.Id, cmd.UserId);

        return Result<CreateBudgetResponse>.Success(new CreateBudgetResponse(budget.Id, budget.Name));
    }
}
