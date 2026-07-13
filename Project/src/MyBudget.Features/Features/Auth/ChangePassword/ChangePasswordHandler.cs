using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.ChangePassword;

public sealed class ChangePasswordHandler
    : IRequestHandler<ChangePasswordCommand, Result<Unit>>
{
    private readonly AppDbContext            _db;
    private readonly ICurrentUserService     _currentUser;
    private readonly ISecurityAuditWriter    _auditWriter;
    private readonly IPasswordPolicyService  _policy;
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(
        AppDbContext            db,
        ICurrentUserService     currentUser,
        ISecurityAuditWriter    auditWriter,
        IPasswordPolicyService  policy,
        ILogger<ChangePasswordHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _auditWriter = auditWriter;
        _policy      = policy;
        _logger      = logger;
    }

    public async ValueTask<Result<Unit>> Handle(
        ChangePasswordCommand cmd, CancellationToken ct)
    {
        // STEP 1 — Resolve current user from JWT claims
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user has no UserId claim.");

        // STEP 2 — Load user via EF
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new InvalidOperationException("Authenticated user not found in database.");

        // STEP 3 — Verify current password
        if (!BCrypt.Net.BCrypt.Verify(cmd.CurrentPassword, user.PasswordHash))
        {
            return Result<Unit>.Failure("PWD_CURRENT_INCORRECT");
        }

        // STEP 4 — Reject if new password matches current hash
        if (BCrypt.Net.BCrypt.Verify(cmd.NewPassword, user.PasswordHash))
            return Result<Unit>.Failure("PWD_SAME_AS_CURRENT");

        // STEP 4b — Reject if new password matches any entry in password history
        if (_policy.PasswordHistoryCount > 0)
        {
            var history = await _db.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(_policy.PasswordHistoryCount)
                .ToListAsync(ct);

            if (history.Any(h => BCrypt.Net.BCrypt.Verify(cmd.NewPassword, h.PasswordHash)))
                return Result<Unit>.Failure("PWD_PREVIOUSLY_USED");
        }

        // STEP 5 — Apply new password (clears ForcePasswordChange, sets PasswordChangedAt)
        var previousHash = user.PasswordHash;
        user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(cmd.NewPassword, workFactor: 12));

        // STEP 5 — Revoke all active refresh tokens except the current session's
        // If CurrentRefreshToken is provided, preserve the matching token; revoke the rest
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var rt in activeTokens)
        {
            // Preserve the current session's token if we can identify it via BCrypt
            if (!string.IsNullOrEmpty(cmd.CurrentRefreshToken)
                && BCrypt.Net.BCrypt.Verify(cmd.CurrentRefreshToken, rt.TokenHash))
            {
                continue; // keep current session
            }

            rt.Revoke();
        }

        // STEP 6 — Record previous hash in password history; prune beyond configured limit
        _db.PasswordHistories.Add(PasswordHistory.Create(userId, previousHash));

        if (_policy.PasswordHistoryCount > 0)
        {
            var oldEntries = await _db.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Skip(_policy.PasswordHistoryCount)
                .ToListAsync(ct);

            if (oldEntries.Count > 0)
                _db.PasswordHistories.RemoveRange(oldEntries);
        }

        // STEP 7 — Persist all changes
        await _db.SaveChangesAsync(ct);

        // STEP 7 — Write audit event
        await _auditWriter.WriteAsync(
            "PasswordChanged",
            userId:  user.Id,
            email:   user.Email,
            ct:      ct);

        _logger.LogInformation(
            "Password changed for user {UserId}", user.Id);

        return Result<Unit>.Success(Unit.Value);
    }
}
