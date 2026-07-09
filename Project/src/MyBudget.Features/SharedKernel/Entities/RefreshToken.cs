namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Hashed refresh token stored per user session.
/// Raw token is returned to the client and NEVER stored in plain text.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    /// <summary>BCrypt hash of the raw random token returned to the client.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    /// <summary>Points to the new token that replaced this one during rotation.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAt)
    {
        return new RefreshToken
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    public bool IsExpired  => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked  => RevokedAt.HasValue;
    public bool IsActive   => !IsExpired && !IsRevoked;

    public void Revoke(Guid? replacedById = null)
    {
        RevokedAt          = DateTime.UtcNow;
        ReplacedByTokenId  = replacedById;
        UpdatedAt          = DateTimeOffset.UtcNow;
    }
}
