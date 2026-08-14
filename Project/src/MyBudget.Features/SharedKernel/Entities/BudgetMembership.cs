namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>Links a user to a budget with a specific role.</summary>
public sealed class BudgetMembership : BaseEntity
{
    public Guid BudgetId { get; private set; }

    public Guid UserId { get; private set; }

    public BudgetRole Role { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public bool IsDeleted { get; private set; } = false;

    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public Budget? Budget { get; private set; }
    public User? User { get; private set; }

    private BudgetMembership() { }

    public static BudgetMembership Create(Guid budgetId, Guid userId, BudgetRole role)
    {
        return new BudgetMembership
        {
            BudgetId  = budgetId,
            UserId    = userId,
            Role      = role,
            JoinedAt  = DateTime.UtcNow,
            IsDeleted = false,
        };
    }

    /// <summary>Soft-deletes (revokes) the membership. Sets IsDeleted = true and records the timestamp.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Restores a soft-deleted membership. Clears IsDeleted and DeletedAt. JoinedAt is untouched — this is a resumed membership, not a new one.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Changes the member's role.</summary>
    public void ChangeRole(BudgetRole newRole)
    {
        Role      = newRole;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
