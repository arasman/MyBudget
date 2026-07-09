using FluentValidation;

namespace MyBudget.Features.Features.Auth.LogoutUser;

public sealed class LogoutUserValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
