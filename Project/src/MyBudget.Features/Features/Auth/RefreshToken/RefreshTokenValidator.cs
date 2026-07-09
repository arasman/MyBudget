using FluentValidation;

namespace MyBudget.Features.Features.Auth.RefreshToken;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
