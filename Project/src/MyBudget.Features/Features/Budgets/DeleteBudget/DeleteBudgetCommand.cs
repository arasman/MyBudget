using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.DeleteBudget;

public sealed record DeleteBudgetCommand(Guid BudgetId, Guid UserId)
    : IRequest<Result<Unit>>;
