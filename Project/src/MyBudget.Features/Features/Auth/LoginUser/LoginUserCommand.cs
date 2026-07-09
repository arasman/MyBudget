using Mediator;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.LoginUser;

public sealed record LoginUserCommand(
    string Email,
    string Password
) : IRequest<Result<LoginResponse>>;
