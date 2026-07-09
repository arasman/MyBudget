using FluentValidation;

namespace MyBudget.Features.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
