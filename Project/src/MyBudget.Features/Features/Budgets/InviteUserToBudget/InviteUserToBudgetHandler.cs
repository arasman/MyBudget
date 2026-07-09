using System.Security.Cryptography;
using Dapper;
using Mediator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Email;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.InviteUserToBudget;

public sealed class InviteUserToBudgetHandler
    : IRequestHandler<InviteUserToBudgetCommand, Result<InviteUserToBudgetResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IEmailSender      _emailSender;
    private readonly IMemoryCache      _cache;
    private readonly IConfiguration    _config;
    private readonly ILogger<InviteUserToBudgetHandler> _logger;

    public InviteUserToBudgetHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IEmailSender emailSender,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<InviteUserToBudgetHandler> logger)
    {
        _db          = db;
        _factory     = factory;
        _emailSender = emailSender;
        _cache       = cache;
        _config      = config;
        _logger      = logger;
    }

    public async ValueTask<Result<InviteUserToBudgetResponse>> Handle(
        InviteUserToBudgetCommand cmd, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // 1. Verify budget exists
        var budgetExists = await conn.ExecuteScalarAsync<bool>(
            """SELECT COUNT(1) > 0 FROM "Budgets" WHERE "Id" = @BudgetId""",
            new { BudgetId = cmd.BudgetId });

        if (!budgetExists)
            return Result<InviteUserToBudgetResponse>.Failure("BUDGET_NOT_FOUND");

        // 2. Check invitee is not already a member
        var normalizedEmail = cmd.InviteeEmail.Trim().ToLowerInvariant();
        var alreadyMember = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT COUNT(1) > 0
            FROM "BudgetMemberships" bm
            JOIN "Users" u ON u."Id" = bm."UserId"
            WHERE bm."BudgetId" = @BudgetId AND u."Email" = @Email
            """,
            new { BudgetId = cmd.BudgetId, Email = normalizedEmail });

        if (alreadyMember)
            return Result<InviteUserToBudgetResponse>.Failure("AUTH_ALREADY_MEMBER");

        // 3. Generate 256-bit random token (32 bytes = 256 bits)
        var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken      = Convert.ToBase64String(rawTokenBytes)
                                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // 4. BCrypt-hash the token for storage
        var tokenHash  = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 6);
        var expiresAt  = DateTime.UtcNow.AddHours(72);

        var invitation = Invitation.Create(
            cmd.BudgetId, cmd.InviteeEmail, cmd.Role, tokenHash, expiresAt, cmd.InvitedByUserId);

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        // 5. Evict cache for the invitee if they are already known (membership may change)
        var inviteeId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """SELECT "Id" FROM "Users" WHERE "Email" = @Email""",
            new { Email = normalizedEmail });

        if (inviteeId.HasValue)
            _cache.Remove($"budget-membership:{inviteeId.Value}:{cmd.BudgetId}");

        // 6. Queue invitation email
        var frontendBaseUrl = _config["App:FrontendBaseUrl"] ?? "http://localhost:5173";
        var acceptLink = $"{frontendBaseUrl}/invitations/accept?token={Uri.EscapeDataString(rawToken)}";

        await _emailSender.SendAsync(new EmailMessage(
            To:      normalizedEmail,
            Subject: "You have been invited to a budget",
            Body:    $"<p>You have been invited to join a budget. " +
                     $"<a href=\"{acceptLink}\">Click here to accept</a>.</p>" +
                     $"<p>This link expires in 72 hours.</p>"), ct);

        _logger.LogInformation(
            "Invitation sent for budget {BudgetId} to {Email}", cmd.BudgetId, normalizedEmail);

        return Result<InviteUserToBudgetResponse>.Success(
            new InviteUserToBudgetResponse(invitation.Id, expiresAt));
    }
}
