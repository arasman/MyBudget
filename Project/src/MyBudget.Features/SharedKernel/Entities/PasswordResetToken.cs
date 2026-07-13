namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Single-use password-reset token. The raw token is sent to the user by email;
/// only its BCrypt hash (workFactor 6) is persisted here — mirrors the Invitation pattern.
/// </summary>
public sealed class PasswordResetToken : BaseEntity
{
    public Guid     UserId    { get; private set; }
    public string   TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt   { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        Guid     userId,
        string   tokenHash,
        DateTime expiresAt)
    {
        return new PasswordResetToken
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed    => UsedAt.HasValue;

    public void MarkUsed()
    {
        UsedAt    = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
