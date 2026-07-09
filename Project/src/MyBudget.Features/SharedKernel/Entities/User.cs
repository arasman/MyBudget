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
        LastLoginAt  = DateTime.UtcNow;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }
}
