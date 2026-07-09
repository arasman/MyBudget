using Mediator;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserResponse>>;
