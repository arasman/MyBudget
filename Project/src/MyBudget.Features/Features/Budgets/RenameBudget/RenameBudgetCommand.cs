using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RenameBudget;

public sealed record RenameBudgetCommand(Guid BudgetId, string NewName, Guid UserId)
    : IRequest<Result<RenameBudgetResponse>>;

public sealed record RenameBudgetResponse(Guid Id, string Name);
