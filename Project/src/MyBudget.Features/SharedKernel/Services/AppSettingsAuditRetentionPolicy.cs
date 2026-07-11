using Microsoft.Extensions.Configuration;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Reads <c>AuditLog:RetentionDays</c> from <see cref="IConfiguration"/>.
/// Defaults to 90 days when the key is absent or unparseable.
/// </summary>
public sealed class AppSettingsAuditRetentionPolicy : IAuditRetentionPolicy
{
    private const int DefaultRetentionDays = 90;

    private readonly IConfiguration _configuration;

    public AppSettingsAuditRetentionPolicy(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int GetRetentionDays()
    {
        var raw = _configuration["AuditLog:RetentionDays"];
        return int.TryParse(raw, out var days) && days > 0
            ? days
            : DefaultRetentionDays;
    }
}
