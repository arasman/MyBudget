using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

/// <summary>
/// Unit tests for password-management domain methods on <see cref="User"/>.
/// Covers T-1.2: RecordFailedLogin, ClearLockout, UpdatePassword, SetForcePasswordChange.
/// </summary>
public sealed class UserPasswordTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static User CreateUser() =>
        User.Create("test@example.com", "hash", "Test", "User");

    // -------------------------------------------------------------------------
    // RecordFailedLogin
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordFailedLogin_BelowThreshold_IncreasesCounterAndDoesNotLock()
    {
        var user = CreateUser();

        var wasLocked = user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        wasLocked.ShouldBeFalse();
        user.FailedLoginAttempts.ShouldBe(1);
        user.LockoutUntil.ShouldBeNull();
    }

    [Fact]
    public void RecordFailedLogin_AtThreshold_LocksAccountAndReturnsTrue()
    {
        var user = CreateUser();
        var before = DateTime.UtcNow;

        // Bring counter to threshold - 1
        for (var i = 0; i < 4; i++)
            user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        // This call should trigger the lockout
        var wasLocked = user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        var after = DateTime.UtcNow;

        wasLocked.ShouldBeTrue();
        user.FailedLoginAttempts.ShouldBe(5);
        user.LockoutUntil.ShouldNotBeNull();
        user.LockoutUntil!.Value.ShouldBeGreaterThan(before.AddMinutes(29));
        user.LockoutUntil!.Value.ShouldBeLessThanOrEqualTo(after.AddMinutes(30));
    }

    [Fact]
    public void RecordFailedLogin_AboveThreshold_CounterIncrements_LockoutExtended()
    {
        var user = CreateUser();

        // Trigger lockout at threshold
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        // Additional call beyond threshold — counter increments, lockout window refreshes, wasLocked = true
        var wasLocked = user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        wasLocked.ShouldBeTrue(); // still >= maxAttempts so wasLocked is true (lockout window refreshed)
        user.FailedLoginAttempts.ShouldBe(6);
        user.LockoutUntil.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // ClearLockout
    // -------------------------------------------------------------------------

    [Fact]
    public void ClearLockout_ResetsCounterAndLockoutUntil()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(maxAttempts: 5, lockoutDurationMinutes: 30);

        user.ClearLockout();

        user.FailedLoginAttempts.ShouldBe(0);
        user.LockoutUntil.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // UpdatePassword
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdatePassword_SetsNewHash_SetsTimestamp_ClearsForceFlag()
    {
        var user = CreateUser();
        user.SetForcePasswordChange();
        var before = DateTime.UtcNow;

        user.UpdatePassword("newHash123");

        var after = DateTime.UtcNow;

        user.PasswordHash.ShouldBe("newHash123");
        user.ForcePasswordChange.ShouldBeFalse();
        user.PasswordChangedAt.ShouldNotBeNull();
        user.PasswordChangedAt!.Value.ShouldBeGreaterThanOrEqualTo(before);
        user.PasswordChangedAt!.Value.ShouldBeLessThanOrEqualTo(after);
    }

    // -------------------------------------------------------------------------
    // SetForcePasswordChange
    // -------------------------------------------------------------------------

    [Fact]
    public void SetForcePasswordChange_SetsFlag()
    {
        var user = CreateUser();

        user.SetForcePasswordChange();

        user.ForcePasswordChange.ShouldBeTrue();
    }
}
