using FluentValidation;

namespace MyBudget.Features.Features.Auth.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MinimumLength(8).WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .MaximumLength(72).WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[A-Z]").WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[a-z]").WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[0-9]").WithErrorCode("PWD_PASSWORD_TOO_WEAK");
    }
}
