using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.InviteUserToBudget;

public sealed record InviteUserToBudgetCommand(
    Guid       BudgetId,
    string     InviteeEmail,
    BudgetRole Role,
    Guid       InvitedByUserId
) : IRequest<Result<InviteUserToBudgetResponse>>;

public sealed record InviteUserToBudgetResponse(Guid InvitationId, DateTime ExpiresAt);
