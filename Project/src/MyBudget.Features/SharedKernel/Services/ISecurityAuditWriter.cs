namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Writes security/auth events to the SecurityAuditLog table.
/// Implementations extract IP address and User-Agent from the HTTP context.
/// </summary>
public interface ISecurityAuditWriter
{
    Task WriteAsync(
        string            eventName,
        Guid?             userId,
        string?           email,
        object?           details          = null,
        CancellationToken ct               = default);
}
