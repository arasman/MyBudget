using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.ResetPassword;

public sealed class ResetPasswordHandler
    : IRequestHandler<ResetPasswordCommand, Result<Unit>>
{
    private readonly AppDbContext            _db;
    private readonly ISecurityAuditWriter    _auditWriter;
    private readonly IPasswordPolicyService  _policy;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        AppDbContext            db,
        ISecurityAuditWriter    auditWriter,
        IPasswordPolicyService  policy,
        ILogger<ResetPasswordHandler> logger)
    {
        _db          = db;
        _auditWriter = auditWriter;
        _policy      = policy;
        _logger      = logger;
    }

    public async ValueTask<Result<Unit>> Handle(
        ResetPasswordCommand cmd, CancellationToken ct)
    {
        // STEP 1 — Load all non-expired, non-used tokens with their associated user
        // BCrypt hashes cannot be indexed, so we filter by ExpiresAt/UsedAt then verify
        var candidateTokens = await _db.PasswordResetTokens
            .Include(t => t.User)
            .Where(t => t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        // STEP 2 — Find matching token via BCrypt.Verify
        PasswordResetToken? matchedToken = null;
        foreach (var candidate in candidateTokens)
        {
            if (BCrypt.Net.BCrypt.Verify(cmd.Token, candidate.TokenHash))
            {
                matchedToken = candidate;
                break;
            }
        }

        // STEP 3 — Token not found or no match
        if (matchedToken is null)
        {
            _logger.LogWarning("Password reset attempted with invalid or unknown token.");
            return Result<Unit>.Failure("PWD_TOKEN_INVALID");
        }

        // STEP 4 — Load user (should be included via navigation)
        var user = matchedToken.User
            ?? await _db.Users.FindAsync([matchedToken.UserId], ct)
            ?? throw new InvalidOperationException("User not found for matched password reset token.");

        // STEP 5 — Reject if new password matches current hash
        if (BCrypt.Net.BCrypt.Verify(cmd.NewPassword, user.PasswordHash))
            return Result<Unit>.Failure("PWD_SAME_AS_CURRENT");

        // STEP 5b — Reject if new password matches any entry in password history
        if (_policy.PasswordHistoryCount > 0)
        {
            var history = await _db.PasswordHistories
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.CreatedAt)
                .Take(_policy.PasswordHistoryCount)
                .ToListAsync(ct);

            if (history.Any(h => BCrypt.Net.BCrypt.Verify(cmd.NewPassword, h.PasswordHash)))
                return Result<Unit>.Failure("PWD_PREVIOUSLY_USED");
        }

        // STEP 6 — Update password (clears lockout, ForcePasswordChange, sets PasswordChangedAt)
        var previousHash = user.PasswordHash;
        user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(cmd.NewPassword, workFactor: 12));
        user.ClearLockout();

        // STEP 6 — Mark token used
        matchedToken.MarkUsed();

        // STEP 7 — Revoke ALL active refresh tokens for the user
        await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow),
                ct);

        // STEP 8 — Record previous hash in password history; prune beyond configured limit
        _db.PasswordHistories.Add(PasswordHistory.Create(user.Id, previousHash));

        if (_policy.PasswordHistoryCount > 0)
        {
            var oldEntries = await _db.PasswordHistories
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.CreatedAt)
                .Skip(_policy.PasswordHistoryCount)
                .ToListAsync(ct);

            if (oldEntries.Count > 0)
                _db.PasswordHistories.RemoveRange(oldEntries);
        }

        // STEP 9 — Persist all changes
        await _db.SaveChangesAsync(ct);

        // STEP 9 — Write audit event
        await _auditWriter.WriteAsync(
            "PasswordChanged",
            userId:  user.Id,
            email:   user.Email,
            ct:      ct);

        _logger.LogInformation(
            "Password reset completed for user {UserId}", user.Id);

        return Result<Unit>.Success(Unit.Value);
    }
}
