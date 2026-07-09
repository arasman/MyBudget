using Mediator;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PreferredLocale = "en"
) : IRequest<Result<LoginResponse>>;
