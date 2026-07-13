namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Provides password security policy values. Resolved from DI as a singleton.
/// All values fall back to documented defaults when the <c>PasswordPolicy</c>
/// appsettings section is absent or a key is missing.
/// </summary>
public interface IPasswordPolicyService
{
    /// <summary>Number of consecutive failed logins that trigger an account lockout. Default: 5.</summary>
    int MaxFailedAttempts { get; }

    /// <summary>Duration of an account lockout in minutes. Default: 30.</summary>
    int LockoutDurationMinutes { get; }

    /// <summary>
    /// Days after which a password must be changed at next login. 0 disables the age check. Default: 365.
    /// </summary>
    int ForceChangeAfterDays { get; }

    /// <summary>Lifetime of a password-reset token in minutes. Default: 30.</summary>
    int ResetTokenExpiryMinutes { get; }

    /// <summary>
    /// Number of previous password hashes to retain and check against. 0 disables history. Default: 5.
    /// </summary>
    int PasswordHistoryCount { get; }
}
