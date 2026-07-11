namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Standalone record for security/auth events (login, logout, registration, etc.).
/// Does NOT extend BaseEntity — it has its own Guid PK.
/// Written explicitly by auth handlers via ISecurityAuditWriter, NOT via SaveChangesAsync.
/// </summary>
public sealed class SecurityAuditLog
{
    public Guid            Id          { get; private set; }
    public string          Event       { get; private set; } = string.Empty;
    public Guid?           UserId      { get; private set; }
    public string?         Email       { get; private set; }
    public string?         IpAddress   { get; private set; }
    public string?         UserAgent   { get; private set; }
    public DateTimeOffset  Timestamp   { get; private set; }
    public string?         Details     { get; private set; }

    private SecurityAuditLog() { }

    public static SecurityAuditLog Create(
        string  eventName,
        Guid?   userId,
        string? email,
        string? ipAddress,
        string? userAgent,
        string? details)
    {
        return new SecurityAuditLog
        {
            Id        = Guid.NewGuid(),
            Event     = eventName,
            UserId    = userId,
            Email     = email,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTimeOffset.UtcNow,
            Details   = details,
        };
    }
}
