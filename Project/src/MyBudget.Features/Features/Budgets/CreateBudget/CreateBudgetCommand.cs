using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.CreateBudget;

public sealed record CreateBudgetCommand(string Name, Guid UserId)
    : IRequest<Result<CreateBudgetResponse>>;

public sealed record CreateBudgetResponse(Guid BudgetId, string Name);
