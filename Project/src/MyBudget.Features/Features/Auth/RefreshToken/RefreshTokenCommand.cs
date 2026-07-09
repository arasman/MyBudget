using Mediator;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    Guid   UserId
) : IRequest<Result<LoginResponse>>;
