using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

/// <summary>
/// Mirrors <see cref="BudgetDomainTests"/>'s SoftDelete/Restore coverage for
/// <see cref="BudgetMembership"/> (budget-member-administration, WU2, task 14.1).
/// </summary>
public sealed class BudgetMembershipDomainTests
{
    private static BudgetMembership BuildMembership() =>
        BudgetMembership.Create(Guid.NewGuid(), Guid.NewGuid(), BudgetRole.Operator);

    // --- SoftDelete ---

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var membership = BuildMembership();
        membership.SoftDelete();
        membership.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void SoftDelete_SetsDeletedAt()
    {
        var membership = BuildMembership();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        membership.SoftDelete();
        membership.DeletedAt.ShouldNotBeNull();
        membership.DeletedAt!.Value.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void SoftDelete_SetsUpdatedAt()
    {
        var membership = BuildMembership();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        membership.SoftDelete();
        membership.UpdatedAt.ShouldNotBeNull();
        membership.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- Restore ---

    [Fact]
    public void Restore_ClearsIsDeleted()
    {
        var membership = BuildMembership();
        membership.SoftDelete();
        membership.Restore();
        membership.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var membership = BuildMembership();
        membership.SoftDelete();
        membership.Restore();
        membership.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_SetsUpdatedAt()
    {
        var membership = BuildMembership();
        membership.SoftDelete();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        membership.Restore();
        membership.UpdatedAt.ShouldNotBeNull();
        membership.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void Restore_LeavesJoinedAtUntouched()
    {
        var membership = BuildMembership();
        var originalJoinedAt = membership.JoinedAt;
        membership.SoftDelete();
        membership.Restore();
        membership.JoinedAt.ShouldBe(originalJoinedAt);
    }

    // --- ChangeRole ---

    [Fact]
    public void ChangeRole_UpdatesRole()
    {
        var membership = BuildMembership();
        membership.ChangeRole(BudgetRole.Admin);
        membership.Role.ShouldBe(BudgetRole.Admin);
    }

    [Fact]
    public void ChangeRole_SetsUpdatedAt()
    {
        var membership = BuildMembership();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        membership.ChangeRole(BudgetRole.Admin);
        membership.UpdatedAt.ShouldNotBeNull();
        membership.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- Create defaults ---

    [Fact]
    public void Create_SetsIsDeletedFalse()
    {
        var membership = BuildMembership();
        membership.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Create_SetsDeletedAtNull()
    {
        var membership = BuildMembership();
        membership.DeletedAt.ShouldBeNull();
    }
}
