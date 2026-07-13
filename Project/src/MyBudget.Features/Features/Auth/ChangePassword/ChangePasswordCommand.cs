using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(
    string  CurrentPassword,
    string  NewPassword,
    string? CurrentRefreshToken
) : IRequest<Result<Unit>>;
