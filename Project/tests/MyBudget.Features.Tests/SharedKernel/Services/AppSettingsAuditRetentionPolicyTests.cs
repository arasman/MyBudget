using Microsoft.Extensions.Configuration;
using MyBudget.Features.SharedKernel.Services;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Services;

/// <summary>
/// Unit tests for <see cref="AppSettingsAuditRetentionPolicy"/>.
/// PR5 tasks 5.2 and 5.3.
/// </summary>
public sealed class AppSettingsAuditRetentionPolicyTests
{
    // -------------------------------------------------------------------------
    // 5.2 — Returns configured value when AuditLog:RetentionDays is present
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRetentionDays_ReturnsConfiguredValue_WhenKeyIsPresent()
    {
        var config = Substitute.For<IConfiguration>();
        config["AuditLog:RetentionDays"].Returns("180");

        var policy = new AppSettingsAuditRetentionPolicy(config);

        policy.GetRetentionDays().ShouldBe(180);
    }

    // -------------------------------------------------------------------------
    // 5.3 — Returns 90 (default) when AuditLog:RetentionDays key is absent
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRetentionDays_ReturnsDefault90_WhenKeyIsAbsent()
    {
        var config = Substitute.For<IConfiguration>();
        config["AuditLog:RetentionDays"].Returns((string?)null);

        var policy = new AppSettingsAuditRetentionPolicy(config);

        policy.GetRetentionDays().ShouldBe(90);
    }
}
