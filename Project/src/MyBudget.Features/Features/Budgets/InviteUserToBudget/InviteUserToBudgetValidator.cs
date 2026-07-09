using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.Budgets.InviteUserToBudget;

public sealed class InviteUserToBudgetValidator : AbstractValidator<InviteUserToBudgetCommand>
{
    public InviteUserToBudgetValidator()
    {
        RuleFor(x => x.InviteeEmail)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .EmailAddress().WithErrorCode("FIELD_INVALID")
            .MaximumLength(254).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.Role)
            .NotEqual(BudgetRole.Owner)
            .WithErrorCode("AUTH_CANNOT_INVITE_AS_OWNER")
            .WithMessage("Cannot invite a user as Owner.");

        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.InvitedByUserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
