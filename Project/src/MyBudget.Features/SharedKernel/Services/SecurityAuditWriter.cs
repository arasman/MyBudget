using Microsoft.AspNetCore.Http;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Writes security/auth events to the SecurityAuditLogs table.
/// Extracts IpAddress from X-Forwarded-For header (proxy-aware) with fallback to
/// RemoteIpAddress, and UserAgent from the User-Agent request header.
/// Saves independently from the business transaction (intentional separate SaveChangesAsync).
/// </summary>
public sealed class SecurityAuditWriter : ISecurityAuditWriter
{
    private readonly AppDbContext        _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SecurityAuditWriter(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db                  = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task WriteAsync(
        string            eventName,
        Guid?             userId,
        string?           email,
        object?           details          = null,
        CancellationToken ct               = default)
    {
        var ctx       = _httpContextAccessor.HttpContext;
        var ipAddress = ExtractIpAddress(ctx);
        var userAgent = ctx?.Request.Headers["User-Agent"].ToString();

        var entry = SecurityAuditLog.Create(
            eventName: eventName,
            userId:    userId,
            email:     email,
            ipAddress: ipAddress,
            userAgent: string.IsNullOrEmpty(userAgent) ? null : userAgent,
            details:   details is null ? null : System.Text.Json.JsonSerializer.Serialize(details));

        _db.SecurityAuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    private static string? ExtractIpAddress(HttpContext? ctx)
    {
        if (ctx is null) return null;

        var forwarded = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // X-Forwarded-For may be a comma-separated list; take the first (client IP)
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }

        return ctx.Connection.RemoteIpAddress?.ToString();
    }
}
