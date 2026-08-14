using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Auth;

/// <summary>
/// Full matrix-cell coverage for <see cref="MemberActionPolicy.Evaluate"/> — every actor role ×
/// target role × newRole? combination relevant to the 5-rule permission matrix (design.md's
/// Architecture Decision #4), asserted in rule order.
/// </summary>
public sealed class MemberActionPolicyTests
{
    private static readonly Guid ActorId  = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    // Rule 1: self-check — fires before every other rule, regardless of role.
    [Theory]
    [InlineData(BudgetRole.Owner, BudgetRole.Owner)]
    [InlineData(BudgetRole.Admin, BudgetRole.Admin)]
    [InlineData(BudgetRole.Admin, BudgetRole.Operator)]
    public void Evaluate_TargetIsActor_ReturnsCannotActOnSelf(BudgetRole actorRole, BudgetRole targetRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, ActorId, targetRole)
            .ShouldBe("MEMBERS_CANNOT_ACT_ON_SELF");
    }

    [Fact]
    public void Evaluate_OwnerActsOnSelf_ReturnsCannotActOnSelf_NotCannotActOnOwner()
    {
        // Self-check fires before the owner-target check — an Owner acting on themselves gets the
        // accurate "self" message, not "owner" (design decision 4, task 6.2).
        MemberActionPolicy.Evaluate(ActorId, BudgetRole.Owner, ActorId, BudgetRole.Owner)
            .ShouldBe("MEMBERS_CANNOT_ACT_ON_SELF");
    }

    // Rule 2: target is Owner — forbidden for any actor, once self-check passes.
    [Theory]
    [InlineData(BudgetRole.Owner)]
    [InlineData(BudgetRole.Admin)]
    public void Evaluate_TargetIsOwner_ReturnsCannotActOnOwner(BudgetRole actorRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, TargetId, BudgetRole.Owner)
            .ShouldBe("MEMBERS_CANNOT_ACT_ON_OWNER");
    }

    // Rule 3: Admin acting on Admin — forbidden. Owner acting on Admin is allowed.
    [Fact]
    public void Evaluate_AdminActorTargetsAdmin_ReturnsCannotActOnAdmin()
    {
        MemberActionPolicy.Evaluate(ActorId, BudgetRole.Admin, TargetId, BudgetRole.Admin)
            .ShouldBe("MEMBERS_CANNOT_ACT_ON_ADMIN");
    }

    [Fact]
    public void Evaluate_OwnerActorTargetsAdmin_IsAllowed()
    {
        MemberActionPolicy.Evaluate(ActorId, BudgetRole.Owner, TargetId, BudgetRole.Admin)
            .ShouldBeNull();
    }

    // Rule 4: promoting to Owner is always rejected, regardless of actor/target role, as long as
    // the earlier rules did not already fire.
    [Theory]
    [InlineData(BudgetRole.Owner, BudgetRole.Operator)]
    [InlineData(BudgetRole.Owner, BudgetRole.ReadOnly)]
    [InlineData(BudgetRole.Owner, BudgetRole.Admin)]
    public void Evaluate_NewRoleIsOwner_ReturnsCannotPromoteToOwner(BudgetRole actorRole, BudgetRole targetRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, TargetId, targetRole, BudgetRole.Owner)
            .ShouldBe("MEMBERS_CANNOT_PROMOTE_TO_OWNER");
    }

    // Rule 5: no membership row for the target (represented as targetRole: null) — checked last.
    [Theory]
    [InlineData(BudgetRole.Owner)]
    [InlineData(BudgetRole.Admin)]
    public void Evaluate_TargetRoleIsNull_ReturnsMembersNotFound(BudgetRole actorRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, TargetId, null)
            .ShouldBe("MEMBERS_NOT_FOUND");
    }

    [Fact]
    public void Evaluate_TargetRoleIsNull_ButNewRoleIsOwner_PromoteCheckWinsOverNotFound()
    {
        // Rule 4 (promote to owner) is checked before rule 5 (not found) — matches design's
        // explicit rule order (self, owner-target, admin-vs-admin, promote-to-owner, not-found).
        MemberActionPolicy.Evaluate(ActorId, BudgetRole.Owner, TargetId, null, BudgetRole.Owner)
            .ShouldBe("MEMBERS_CANNOT_PROMOTE_TO_OWNER");
    }

    // Allowed combinations — every cell that should return null (no error).
    [Theory]
    [InlineData(BudgetRole.Owner, BudgetRole.Admin,    BudgetRole.Operator)]
    [InlineData(BudgetRole.Owner, BudgetRole.Admin,    BudgetRole.ReadOnly)]
    [InlineData(BudgetRole.Owner, BudgetRole.Operator, BudgetRole.Admin)]
    [InlineData(BudgetRole.Owner, BudgetRole.Operator, BudgetRole.ReadOnly)]
    [InlineData(BudgetRole.Owner, BudgetRole.ReadOnly, BudgetRole.Operator)]
    [InlineData(BudgetRole.Owner, BudgetRole.ReadOnly, BudgetRole.Admin)]
    [InlineData(BudgetRole.Admin, BudgetRole.Operator, BudgetRole.Admin)]
    [InlineData(BudgetRole.Admin, BudgetRole.Operator, BudgetRole.ReadOnly)]
    [InlineData(BudgetRole.Admin, BudgetRole.ReadOnly,  BudgetRole.Operator)]
    [InlineData(BudgetRole.Admin, BudgetRole.ReadOnly,  BudgetRole.Admin)]
    public void Evaluate_AllowedCombination_ReturnsNull(
        BudgetRole actorRole, BudgetRole targetRole, BudgetRole newRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, TargetId, targetRole, newRole)
            .ShouldBeNull();
    }

    // Non-mutating lookups (e.g. Remove/Restore in a future PR) pass newRole: null — allowed cells
    // must still return null when there is no role change involved.
    [Theory]
    [InlineData(BudgetRole.Owner, BudgetRole.Admin)]
    [InlineData(BudgetRole.Owner, BudgetRole.Operator)]
    [InlineData(BudgetRole.Owner, BudgetRole.ReadOnly)]
    [InlineData(BudgetRole.Admin, BudgetRole.Operator)]
    [InlineData(BudgetRole.Admin, BudgetRole.ReadOnly)]
    public void Evaluate_NoNewRole_AllowedCombination_ReturnsNull(BudgetRole actorRole, BudgetRole targetRole)
    {
        MemberActionPolicy.Evaluate(ActorId, actorRole, TargetId, targetRole)
            .ShouldBeNull();
    }
}
