namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// No-op implementation of <see cref="ISecurityAuditWriter"/>.
/// Used for PR1 compilation. Replaced by <c>SecurityAuditWriter</c> in PR3.
/// </summary>
internal sealed class NullSecurityAuditWriter : ISecurityAuditWriter
{
    public Task WriteAsync(
        string            eventName,
        Guid?             userId,
        string?           email,
        object?           details          = null,
        CancellationToken ct               = default)
        => Task.CompletedTask;
}
