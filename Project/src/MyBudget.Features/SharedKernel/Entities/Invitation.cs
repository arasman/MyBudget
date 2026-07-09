namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>Budget invitation sent by email. Token is stored as a BCrypt hash.</summary>
public sealed class Invitation : BaseEntity
{
    public Guid BudgetId { get; private set; }

    public string InviteeEmail { get; private set; } = string.Empty;

    public BudgetRole Role { get; private set; }

    /// <summary>BCrypt hash of the raw invitation token sent in the email link.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? UsedAt { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    // Navigation
    public Budget? Budget { get; private set; }
    public User? InvitedByUser { get; private set; }

    private Invitation() { }

    public static Invitation Create(
        Guid budgetId,
        string inviteeEmail,
        BudgetRole role,
        string tokenHash,
        DateTime expiresAt,
        Guid invitedByUserId)
    {
        return new Invitation
        {
            BudgetId        = budgetId,
            InviteeEmail    = inviteeEmail.Trim().ToLowerInvariant(),
            Role            = role,
            TokenHash       = tokenHash,
            ExpiresAt       = expiresAt,
            InvitedByUserId = invitedByUserId,
        };
    }

    public bool IsExpired  => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed     => UsedAt.HasValue;

    public void MarkUsed()
    {
        UsedAt    = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
