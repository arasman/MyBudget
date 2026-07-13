using FluentValidation;

namespace MyBudget.Features.Features.Auth.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MinimumLength(8).WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .MaximumLength(72).WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[A-Z]").WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[a-z]").WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Matches("[0-9]").WithErrorCode("PWD_PASSWORD_TOO_WEAK")
            .Must((cmd, newPwd) => newPwd != cmd.CurrentPassword)
            .WithErrorCode("PWD_SAME_AS_CURRENT");
    }
}
