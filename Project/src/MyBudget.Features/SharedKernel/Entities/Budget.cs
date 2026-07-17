namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>A named budget owned by a user. Created automatically at registration.</summary>
public sealed class Budget : BaseEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public bool IsDeleted { get; private set; } = false;

    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public User? Owner { get; private set; }
    public ICollection<BudgetMembership> Memberships { get; private set; } = new List<BudgetMembership>();
    public ICollection<Invitation> Invitations { get; private set; } = new List<Invitation>();

    private Budget() { }

    public Guid? ResolveBudgetId() => Id;

    public static Budget Create(string name, Guid ownerId)
    {
        return new Budget
        {
            Name      = name.Trim(),
            OwnerId   = ownerId,
            IsDeleted = false,
        };
    }

    /// <summary>Renames the budget. Trims leading/trailing whitespace.</summary>
    public void Rename(string newName)
    {
        Name      = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Soft-deletes the budget. Sets IsDeleted = true and records deletion timestamp.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Restores a soft-deleted budget. Clears IsDeleted and DeletedAt.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
