using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RestoreBudgetMember;

public sealed record RestoreBudgetMemberCommand(
    Guid BudgetId,
    Guid TargetUserId,
    Guid ActorUserId
) : IRequest<Result<RestoreBudgetMemberResponse>>;

public sealed record RestoreBudgetMemberResponse(Guid UserId, string Role);
