using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandler
    : IRequestHandler<AcceptInvitationCommand, Result<AcceptInvitationResponse>>
{
    private readonly AppDbContext         _db;
    private readonly ConnectionFactory    _factory;
    private readonly IMemoryCache         _cache;
    private readonly ISecurityAuditWriter _auditWriter;
    private readonly ILogger<AcceptInvitationHandler> _logger;

    public AcceptInvitationHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ISecurityAuditWriter auditWriter,
        ILogger<AcceptInvitationHandler> logger)
    {
        _db          = db;
        _factory     = factory;
        _cache       = cache;
        _auditWriter = auditWriter;
        _logger      = logger;
    }

    public async ValueTask<Result<AcceptInvitationResponse>> Handle(
        AcceptInvitationCommand cmd, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // 1. Fetch current user's email
        var userEmail = await conn.QuerySingleOrDefaultAsync<string>(
            """SELECT "Email" FROM "Users" WHERE "Id" = @UserId""",
            new { UserId = cmd.UserId });

        if (userEmail is null)
            return Result<AcceptInvitationResponse>.Failure("AUTH_INVITATION_NOT_FOUND");

        // 2. Fetch all unused invitations and find match via BCrypt
        var candidates = (await conn.QueryAsync<InvitationRow>(
            """
            SELECT "Id", "BudgetId", "InviteeEmail", "Role", "TokenHash", "ExpiresAt", "UsedAt"
            FROM "Invitations"
            WHERE "UsedAt" IS NULL
            """)).ToList();

        InvitationRow? matched = null;
        foreach (var c in candidates)
        {
            if (BCrypt.Net.BCrypt.Verify(cmd.Token, c.TokenHash))
            {
                matched = c;
                break;
            }
        }

        if (matched is null)
            return Result<AcceptInvitationResponse>.Failure("AUTH_INVITATION_NOT_FOUND");

        // 3. Check expiry
        if (matched.ExpiresAt < DateTime.UtcNow)
            return Result<AcceptInvitationResponse>.Failure("AUTH_INVITATION_EXPIRED");

        // 4. Check already used (redundant with query but safe)
        if (matched.UsedAt.HasValue)
            return Result<AcceptInvitationResponse>.Failure("AUTH_INVITATION_ALREADY_USED");

        // 5. Email match check (case-insensitive)
        if (!string.Equals(matched.InviteeEmail, userEmail, StringComparison.OrdinalIgnoreCase))
            return Result<AcceptInvitationResponse>.Failure("AUTH_INVITATION_EMAIL_MISMATCH");

        // 6. Duplicate-membership guard — must run BEFORE MarkUsed() so a duplicate click never
        // burns the token, and BEFORE the insert so the unique index never fires (design decision 1).
        // WU2 extension: a SOFT-DELETED existing row is restored in place (not treated as a
        // duplicate) — the unique index on (BudgetId, UserId) is total, so a second insert would
        // violate it; restoring keeps membership history intact instead of splitting it.
        var existing = await _db.BudgetMemberships.FirstOrDefaultAsync(
            m => m.BudgetId == matched.BudgetId && m.UserId == cmd.UserId, ct);

        if (existing is not null && !existing.IsDeleted)
            return Result<AcceptInvitationResponse>.Failure("AUTH_ALREADY_MEMBER");

        // 7. Mark invitation used + restore-in-place or create BudgetMembership via EF
        var invitation = await _db.Invitations.FindAsync([matched.Id], ct)
                         ?? throw new InvalidOperationException("Invitation not found in EF context.");
        invitation.MarkUsed();

        if (existing is not null)
        {
            // Soft-deleted membership — restore it and set its role from the NEW invitation
            // (not the pre-removal role). JoinedAt is deliberately left untouched (design decision 8).
            existing.Restore();
            existing.ChangeRole((BudgetRole)matched.Role);
        }
        else
        {
            var membership = BudgetMembership.Create(matched.BudgetId, cmd.UserId, (BudgetRole)matched.Role);
            _db.BudgetMemberships.Add(membership);
        }

        await _db.SaveChangesAsync(ct);

        await _auditWriter.WriteAsync(
            "InvitationAccepted",
            userId: cmd.UserId,
            email:  userEmail,
            ct:     ct);

        // 7. Evict cache
        _cache.Remove($"budget-membership:{cmd.UserId}:{matched.BudgetId}");

        _logger.LogInformation(
            "Invitation {InvitationId} accepted by user {UserId} for budget {BudgetId}",
            matched.Id, cmd.UserId, matched.BudgetId);

        return Result<AcceptInvitationResponse>.Success(
            new AcceptInvitationResponse(matched.BudgetId, ((BudgetRole)matched.Role).ToApiString()));
    }

    private sealed record InvitationRow(
        Guid      Id,
        Guid      BudgetId,
        string    InviteeEmail,
        int       Role,
        string    TokenHash,
        DateTime  ExpiresAt,
        DateTime? UsedAt);
}
