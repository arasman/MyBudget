using FluentValidation;

namespace MyBudget.Features.Features.Auth.LoginUser;

public sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .EmailAddress().WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
