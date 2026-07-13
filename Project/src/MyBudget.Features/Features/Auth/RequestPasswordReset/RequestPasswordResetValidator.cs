using FluentValidation;

namespace MyBudget.Features.Features.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .EmailAddress().WithErrorCode("FIELD_INVALID");
    }
}
