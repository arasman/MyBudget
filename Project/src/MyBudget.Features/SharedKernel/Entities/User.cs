namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>Application user. Inherits BaseEntity for Id, CreatedAt, UpdatedAt, and domain events.</summary>
public sealed class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string PreferredLocale { get; private set; } = "en";

    public DateTime? LastLoginAt { get; private set; }

    // --- Password management fields ---
    public int      FailedLoginAttempts { get; private set; } = 0;
    public DateTime? LockoutUntil        { get; private set; }
    public DateTime? PasswordChangedAt   { get; private set; }
    public bool      ForcePasswordChange { get; private set; } = false;

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    // EF Core requires a parameterless constructor
    private User() { }

    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string preferredLocale = "en")
    {
        return new User
        {
            Email           = email.Trim().ToLowerInvariant(),
            PasswordHash    = passwordHash,
            FirstName       = firstName.Trim(),
            LastName        = lastName.Trim(),
            PreferredLocale = preferredLocale,
        };
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Increments the failed login counter. If the counter reaches <paramref name="maxAttempts"/>,
    /// the account is locked for <paramref name="lockoutDurationMinutes"/> minutes.
    /// </summary>
    /// <returns><c>true</c> if this call triggered a new lockout; otherwise <c>false</c>.</returns>
    public bool RecordFailedLogin(int maxAttempts, int lockoutDurationMinutes = 30)
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTimeOffset.UtcNow;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
            return true;
        }

        return false;
    }

    /// <summary>Clears lockout state after a successful password reset or manual admin action.</summary>
    public void ClearLockout()
    {
        FailedLoginAttempts = 0;
        LockoutUntil        = null;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }

    /// <summary>Sets a new password hash, records the change timestamp, and clears any forced-change flag.</summary>
    public void UpdatePassword(string newHash)
    {
        PasswordHash        = newHash;
        PasswordChangedAt   = DateTime.UtcNow;
        ForcePasswordChange = false;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the account so that the next login is blocked until the password is changed.</summary>
    public void SetForcePasswordChange()
    {
        ForcePasswordChange = true;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }
}
