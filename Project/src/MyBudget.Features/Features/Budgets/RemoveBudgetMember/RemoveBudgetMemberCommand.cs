using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RemoveBudgetMember;

public sealed record RemoveBudgetMemberCommand(
    Guid BudgetId,
    Guid TargetUserId,
    Guid ActorUserId
) : IRequest<Result<Unit>>;
