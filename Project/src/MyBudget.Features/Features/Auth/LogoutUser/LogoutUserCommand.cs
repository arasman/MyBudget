using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.LogoutUser;

public sealed record LogoutUserCommand(
    string RefreshToken,
    Guid   UserId
) : IRequest<Result<bool>>;
