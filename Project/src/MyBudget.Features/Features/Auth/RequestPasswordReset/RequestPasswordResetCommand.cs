using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email)
    : IRequest<Result<Unit>>;
