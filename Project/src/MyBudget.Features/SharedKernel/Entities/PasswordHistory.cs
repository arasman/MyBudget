namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Records a previously used password hash for a user.
/// Used to prevent password reuse within the configured history window.
/// </summary>
public sealed class PasswordHistory
{
    public Guid     Id           { get; private set; }
    public Guid     UserId       { get; private set; }
    public string   PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAt    { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private PasswordHistory() { }

    public static PasswordHistory Create(Guid userId, string passwordHash) =>
        new()
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            PasswordHash = passwordHash,
            CreatedAt    = DateTime.UtcNow,
        };
}
