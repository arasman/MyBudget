using Microsoft.Extensions.Configuration;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Reads password policy from the <c>PasswordPolicy</c> configuration section.
/// Defaults apply when the section or individual keys are absent.
/// </summary>
public sealed class AppSettingsPasswordPolicyService : IPasswordPolicyService
{
    private const int DefaultMaxFailedAttempts     = 5;
    private const int DefaultLockoutDurationMinutes = 30;
    private const int DefaultForceChangeAfterDays   = 365;
    private const int DefaultResetTokenExpiryMinutes = 30;
    private const int DefaultPasswordHistoryCount     = 5;

    private readonly IConfiguration _configuration;

    public AppSettingsPasswordPolicyService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int MaxFailedAttempts =>
        Parse("PasswordPolicy:MaxFailedAttempts", DefaultMaxFailedAttempts);

    public int LockoutDurationMinutes =>
        Parse("PasswordPolicy:LockoutDurationMinutes", DefaultLockoutDurationMinutes);

    public int ForceChangeAfterDays =>
        Parse("PasswordPolicy:ForceChangeAfterDays", DefaultForceChangeAfterDays);

    public int ResetTokenExpiryMinutes =>
        Parse("PasswordPolicy:ResetTokenExpiryMinutes", DefaultResetTokenExpiryMinutes);

    public int PasswordHistoryCount =>
        Parse("PasswordPolicy:PasswordHistoryCount", DefaultPasswordHistoryCount);

    private int Parse(string key, int defaultValue)
    {
        var raw = _configuration[key];
        return int.TryParse(raw, out var value) && value >= 0
            ? value
            : defaultValue;
    }
}
