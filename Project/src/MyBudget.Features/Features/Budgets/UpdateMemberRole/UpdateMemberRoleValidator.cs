using FluentValidation;

namespace MyBudget.Features.Features.Budgets.UpdateMemberRole;

/// <summary>
/// Shape-only validation. Business rules (self-action, owner-target, admin-vs-admin,
/// promote-to-owner, not-found) live in <c>MemberActionPolicy</c> so the resulting error codes
/// match specs/budget-members/spec.md's tested contract exactly (design decision 4).
/// </summary>
public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
