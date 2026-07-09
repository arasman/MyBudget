namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>A named budget owned by a user. Created automatically at registration.</summary>
public sealed class Budget : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    // Navigation
    public User? Owner { get; private set; }
    public ICollection<BudgetMembership> Memberships { get; private set; } = new List<BudgetMembership>();
    public ICollection<Invitation> Invitations { get; private set; } = new List<Invitation>();

    private Budget() { }

    public static Budget Create(string name, Guid ownerId)
    {
        return new Budget
        {
            Name    = name.Trim(),
            OwnerId = ownerId,
        };
    }
}
