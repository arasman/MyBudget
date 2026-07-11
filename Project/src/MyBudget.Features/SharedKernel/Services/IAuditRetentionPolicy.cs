namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Provides the TTL (in days) for audit log retention.
/// The background cleanup service depends solely on this abstraction.
/// </summary>
public interface IAuditRetentionPolicy
{
    int GetRetentionDays();
}
