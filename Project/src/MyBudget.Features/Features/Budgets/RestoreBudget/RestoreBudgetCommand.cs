using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RestoreBudget;

public sealed record RestoreBudgetCommand(Guid BudgetId, Guid UserId)
    : IRequest<Result<RestoreBudgetResponse>>;

public sealed record RestoreBudgetResponse(Guid Id, string Name);
