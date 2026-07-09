using Dapper;
using Mediator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandler
    : IRequestHandler<AcceptInvitationCommand, Result<AcceptInvitationResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<AcceptInvitationHandler> _logger;

    public AcceptInvitationHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<AcceptInvitationHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
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

        // 6. Mark invitation used + create BudgetMembership via EF
        var invitation = await _db.Invitations.FindAsync([matched.Id], ct)
                         ?? throw new InvalidOperationException("Invitation not found in EF context.");
        invitation.MarkUsed();

        var membership = BudgetMembership.Create(matched.BudgetId, cmd.UserId, (BudgetRole)matched.Role);
        _db.BudgetMemberships.Add(membership);

        await _db.SaveChangesAsync(ct);

        // 7. Evict cache
        _cache.Remove($"budget-membership:{cmd.UserId}:{matched.BudgetId}");

        _logger.LogInformation(
            "Invitation {InvitationId} accepted by user {UserId} for budget {BudgetId}",
            matched.Id, cmd.UserId, matched.BudgetId);

        return Result<AcceptInvitationResponse>.Success(
            new AcceptInvitationResponse(matched.BudgetId, ((BudgetRole)matched.Role).ToString()));
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
