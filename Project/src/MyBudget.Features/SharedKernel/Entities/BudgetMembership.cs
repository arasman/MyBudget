namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>Links a user to a budget with a specific role.</summary>
public sealed class BudgetMembership : BaseEntity
{
    public Guid BudgetId { get; private set; }

    public Guid UserId { get; private set; }

    public BudgetRole Role { get; private set; }

    public DateTime JoinedAt { get; private set; }

    // Navigation
    public Budget? Budget { get; private set; }
    public User? User { get; private set; }

    private BudgetMembership() { }

    public static BudgetMembership Create(Guid budgetId, Guid userId, BudgetRole role)
    {
        return new BudgetMembership
        {
            BudgetId = budgetId,
            UserId   = userId,
            Role     = role,
            JoinedAt = DateTime.UtcNow,
        };
    }
}
