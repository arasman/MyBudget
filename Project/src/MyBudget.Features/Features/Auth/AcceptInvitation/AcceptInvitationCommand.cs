using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.AcceptInvitation;

public sealed record AcceptInvitationCommand(
    string Token,
    Guid   UserId
) : IRequest<Result<AcceptInvitationResponse>>;

public sealed record AcceptInvitationResponse(Guid BudgetId, string Role);
