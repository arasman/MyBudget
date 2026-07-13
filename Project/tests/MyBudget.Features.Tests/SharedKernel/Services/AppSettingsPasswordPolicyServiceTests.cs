using Microsoft.Extensions.Configuration;
using MyBudget.Features.SharedKernel.Services;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Services;

/// <summary>
/// Unit tests for <see cref="AppSettingsPasswordPolicyService"/>.
/// Covers T-1.8: reads configured values and falls back to defaults when keys are absent.
/// </summary>
public sealed class AppSettingsPasswordPolicyServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AppSettingsPasswordPolicyService BuildService(
        string? maxAttempts           = null,
        string? lockoutDurationMinutes = null,
        string? forceChangeAfterDays  = null,
        string? resetTokenExpiryMinutes = null)
    {
        var config = Substitute.For<IConfiguration>();
        config["PasswordPolicy:MaxFailedAttempts"].Returns(maxAttempts);
        config["PasswordPolicy:LockoutDurationMinutes"].Returns(lockoutDurationMinutes);
        config["PasswordPolicy:ForceChangeAfterDays"].Returns(forceChangeAfterDays);
        config["PasswordPolicy:ResetTokenExpiryMinutes"].Returns(resetTokenExpiryMinutes);
        return new AppSettingsPasswordPolicyService(config);
    }

    // -------------------------------------------------------------------------
    // Configured values
    // -------------------------------------------------------------------------

    [Fact]
    public void MaxFailedAttempts_ReturnsConfiguredValue()
    {
        var sut = BuildService(maxAttempts: "3");
        sut.MaxFailedAttempts.ShouldBe(3);
    }

    [Fact]
    public void LockoutDurationMinutes_ReturnsConfiguredValue()
    {
        var sut = BuildService(lockoutDurationMinutes: "15");
        sut.LockoutDurationMinutes.ShouldBe(15);
    }

    [Fact]
    public void ForceChangeAfterDays_ReturnsConfiguredValue()
    {
        var sut = BuildService(forceChangeAfterDays: "180");
        sut.ForceChangeAfterDays.ShouldBe(180);
    }

    [Fact]
    public void ResetTokenExpiryMinutes_ReturnsConfiguredValue()
    {
        var sut = BuildService(resetTokenExpiryMinutes: "60");
        sut.ResetTokenExpiryMinutes.ShouldBe(60);
    }

    // -------------------------------------------------------------------------
    // Defaults when keys are absent
    // -------------------------------------------------------------------------

    [Fact]
    public void MaxFailedAttempts_ReturnsDefault5_WhenKeyAbsent()
    {
        var sut = BuildService();
        sut.MaxFailedAttempts.ShouldBe(5);
    }

    [Fact]
    public void LockoutDurationMinutes_ReturnsDefault30_WhenKeyAbsent()
    {
        var sut = BuildService();
        sut.LockoutDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void ForceChangeAfterDays_ReturnsDefault365_WhenKeyAbsent()
    {
        var sut = BuildService();
        sut.ForceChangeAfterDays.ShouldBe(365);
    }

    [Fact]
    public void ResetTokenExpiryMinutes_ReturnsDefault30_WhenKeyAbsent()
    {
        var sut = BuildService();
        sut.ResetTokenExpiryMinutes.ShouldBe(30);
    }

    // -------------------------------------------------------------------------
    // Zero is a valid value (e.g. ForceChangeAfterDays=0 disables age check)
    // -------------------------------------------------------------------------

    [Fact]
    public void ForceChangeAfterDays_Returns0_WhenConfiguredToZero()
    {
        var sut = BuildService(forceChangeAfterDays: "0");
        sut.ForceChangeAfterDays.ShouldBe(0);
    }
}
