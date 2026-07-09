using FluentValidation;

namespace MyBudget.Features.Features.Auth.RegisterUser;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    private static readonly string[] SupportedLocales = ["en", "es"];

    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .EmailAddress().WithErrorCode("FIELD_INVALID")
            .MaximumLength(254).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MinimumLength(8).WithErrorCode("AUTH_PASSWORD_TOO_WEAK")
            .MaximumLength(72).WithErrorCode("AUTH_PASSWORD_TOO_WEAK")
            .Matches("[A-Z]").WithErrorCode("AUTH_PASSWORD_TOO_WEAK")
            .Matches("[a-z]").WithErrorCode("AUTH_PASSWORD_TOO_WEAK")
            .Matches("[0-9]").WithErrorCode("AUTH_PASSWORD_TOO_WEAK");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(100).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.LastName)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(100).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.PreferredLocale)
            .Must(l => string.IsNullOrEmpty(l) || SupportedLocales.Contains(l))
            .WithErrorCode("AUTH_LOCALE_UNSUPPORTED")
            .When(x => !string.IsNullOrEmpty(x.PreferredLocale));
    }
}
