using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.UpdateMemberRole;

public sealed record UpdateMemberRoleCommand(
    Guid       BudgetId,
    Guid       TargetUserId,
    BudgetRole NewRole,
    Guid       ActorUserId
) : IRequest<Result<UpdateMemberRoleResponse>>;

public sealed record UpdateMemberRoleResponse(Guid UserId, string Role);
