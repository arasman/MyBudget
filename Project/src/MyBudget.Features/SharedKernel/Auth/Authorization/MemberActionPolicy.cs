using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Auth.Authorization;

/// <summary>
/// Pure permission matrix shared by every member-mutating action (role change, remove, restore).
/// No I/O — callers resolve the actor's and target's current roles (via one Dapper round trip)
/// and pass them in. <paramref name="targetRole"/> is <c>null</c> when the target has no
/// membership row for the budget at all (rule 5).
/// </summary>
/// <remarks>
/// Rule order (design.md's Architecture Decision #4 / Permission Matrix, and specs/budget-members
/// spec.md's MEMBERS-ROLE-1 scenarios):
/// <list type="number">
/// <item>Self-check first, so an Owner acting on themselves gets the accurate "self" message
/// instead of the "owner" one.</item>
/// <item>Target is the budget's Owner — never actionable by anyone.</item>
/// <item>An Admin acting on another Admin — never actionable by an Admin (Owner may).</item>
/// <item>Promoting to Owner — never assignable via this matrix, regardless of actor role.</item>
/// <item>No membership row for the target — checked last so the more specific rules above win
/// when they also apply (e.g. promote-to-owner on a not-found target still reports the
/// promote-to-owner error).</item>
/// </list>
/// </remarks>
public static class MemberActionPolicy
{
    public static string? Evaluate(
        Guid actorId,
        BudgetRole actorRole,
        Guid targetUserId,
        BudgetRole? targetRole,
        BudgetRole? newRole = null)
    {
        if (targetUserId == actorId)
            return "MEMBERS_CANNOT_ACT_ON_SELF";

        if (targetRole == BudgetRole.Owner)
            return "MEMBERS_CANNOT_ACT_ON_OWNER";

        if (actorRole == BudgetRole.Admin && targetRole == BudgetRole.Admin)
            return "MEMBERS_CANNOT_ACT_ON_ADMIN";

        if (newRole == BudgetRole.Owner)
            return "MEMBERS_CANNOT_PROMOTE_TO_OWNER";

        if (targetRole is null)
            return "MEMBERS_NOT_FOUND";

        return null;
    }
}
