namespace MyBudget.Features.SharedKernel.Auth;

/// <summary>
/// JWT configuration options. Key is provided via User Secrets (dev) or env var (prod).
/// Non-secret fields (Issuer, Audience, expiry) are safe to commit in appsettings.json.
/// </summary>
public sealed record JwtOptions
{
    /// <summary>Signing key — MUST be set via User Secrets or JWT__Key env var. Never in appsettings.json.</summary>
    public string Key { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Access token lifetime in minutes. Default: 15.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 15;
}
