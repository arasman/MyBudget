using FluentValidation;

namespace MyBudget.Features.Features.Auth.UpdateLocale;

public sealed class UpdateLocaleValidator : AbstractValidator<UpdateLocaleCommand>
{
    public UpdateLocaleValidator()
    {
        RuleFor(x => x.Locale)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
