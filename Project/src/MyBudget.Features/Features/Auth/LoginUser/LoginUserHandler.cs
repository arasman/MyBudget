using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Entities;
using RefreshTokenEntity = MyBudget.Features.SharedKernel.Entities.RefreshToken;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.LoginUser;

public sealed class LoginUserHandler
    : IRequestHandler<LoginUserCommand, Result<LoginResponse>>
{
    private readonly AppDbContext             _db;
    private readonly ConnectionFactory        _factory;
    private readonly JwtTokenService          _jwt;
    private readonly ISecurityAuditWriter     _auditWriter;
    private readonly IPasswordPolicyService   _policy;
    private readonly ILogger<LoginUserHandler> _logger;

    public LoginUserHandler(
        AppDbContext             db,
        ConnectionFactory        factory,
        JwtTokenService          jwt,
        ISecurityAuditWriter     auditWriter,
        IPasswordPolicyService   policy,
        ILogger<LoginUserHandler> logger)
    {
        _db          = db;
        _factory     = factory;
        _jwt         = jwt;
        _auditWriter = auditWriter;
        _policy      = policy;
        _logger      = logger;
    }

    public async ValueTask<Result<LoginResponse>> Handle(
        LoginUserCommand cmd, CancellationToken ct)
    {
        // STEP 1 — Dapper read for credential lookup (includes lockout fields)
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        using var conn = _factory.CreateConnection();

        var row = await conn.QuerySingleOrDefaultAsync<UserRow>(
            """
            SELECT "Id", "Email", "PasswordHash", "FirstName", "LastName", "PreferredLocale",
                   "LockoutUntil"
            FROM "Users"
            WHERE "Email" = @Email
            LIMIT 1
            """,
            new { Email = normalizedEmail });

        // STEP 1b — Lockout check BEFORE BCrypt (prevents timing side-channel)
        if (row is not null
            && row.LockoutUntil.HasValue
            && row.LockoutUntil.Value > DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure("AUTH_ACCOUNT_LOCKED");
        }

        // STEP 2 — BCrypt.Verify (same response for unknown email and wrong password — no enumeration)
        if (row is null || !BCrypt.Net.BCrypt.Verify(cmd.Password, row.PasswordHash))
        {
            await _auditWriter.WriteAsync(
                "FailedLogin",
                userId: row?.Id,
                email:  normalizedEmail,
                ct:     ct);

            // Load EF entity to record failed attempt (only when user exists)
            if (row is not null)
            {
                var failedUser = await _db.Users.FindAsync([row.Id], ct)
                                 ?? throw new InvalidOperationException("User disappeared between Dapper read and EF load.");

                var wasLocked = failedUser.RecordFailedLogin(
                    _policy.MaxFailedAttempts,
                    _policy.LockoutDurationMinutes);

                if (wasLocked)
                {
                    await _auditWriter.WriteAsync(
                        "AccountLocked",
                        userId: failedUser.Id,
                        email:  failedUser.Email,
                        details: new { FailedAttempts = failedUser.FailedLoginAttempts },
                        ct:     ct);
                }

                await _db.SaveChangesAsync(ct);
            }

            return Result<LoginResponse>.Failure("AUTH_INVALID_CREDENTIALS");
        }

        // STEP 3 — BCrypt success: load EF entity for mutations
        var user = await _db.Users.FindAsync([row.Id], ct)
                   ?? throw new InvalidOperationException("User not found after login check.");

        user.ClearLockout();
        await _db.SaveChangesAsync(ct);

        // STEP 4 — Forced-change check (block token issuance when required)
        var baseline = user.PasswordChangedAt ?? user.CreatedAt.UtcDateTime;
        var ageExceeded = _policy.ForceChangeAfterDays > 0
                          && (DateTime.UtcNow - baseline).TotalDays >= _policy.ForceChangeAfterDays;

        if (user.ForcePasswordChange || ageExceeded)
        {
            return Result<LoginResponse>.Failure("AUTH_FORCE_PASSWORD_CHANGE");
        }

        // STEP 5 — Issue tokens (unchanged from original flow)
        user.UpdateLastLogin();
        await _db.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user);
        var rawRefresh  = _jwt.GenerateRefreshToken();

        var refreshHash  = BCrypt.Net.BCrypt.HashPassword(rawRefresh, workFactor: 6);
        var refreshToken = RefreshTokenEntity.Create(user.Id, refreshHash, DateTime.UtcNow.AddDays(7));
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        await _auditWriter.WriteAsync(
            "SuccessfulLogin",
            userId: user.Id,
            email:  user.Email,
            ct:     ct);

        _logger.LogInformation("User logged in: {UserId}", user.Id);

        var response = new LoginResponse(
            AccessToken:  accessToken,
            RefreshToken: rawRefresh,
            ExpiresIn:    15 * 60,
            User: new UserProfile(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PreferredLocale));

        return Result<LoginResponse>.Success(response);
    }

    // Lightweight Dapper projection — includes only fields needed for lockout pre-check
    private sealed record UserRow(
        Guid      Id,
        string    Email,
        string    PasswordHash,
        string    FirstName,
        string    LastName,
        string    PreferredLocale,
        DateTime? LockoutUntil);
}
