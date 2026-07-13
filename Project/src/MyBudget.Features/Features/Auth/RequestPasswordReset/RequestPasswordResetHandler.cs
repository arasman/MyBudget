using System.Security.Cryptography;
using Dapper;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Email;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetHandler
    : IRequestHandler<RequestPasswordResetCommand, Result<Unit>>
{
    private readonly ConnectionFactory      _factory;
    private readonly AppDbContext           _db;
    private readonly IPasswordPolicyService _policy;
    private readonly IEmailSender           _emailSender;
    private readonly IConfiguration         _config;
    private readonly ILogger<RequestPasswordResetHandler> _logger;

    public RequestPasswordResetHandler(
        ConnectionFactory      factory,
        AppDbContext           db,
        IPasswordPolicyService policy,
        IEmailSender           emailSender,
        IConfiguration         config,
        ILogger<RequestPasswordResetHandler> logger)
    {
        _factory     = factory;
        _db          = db;
        _policy      = policy;
        _emailSender = emailSender;
        _config      = config;
        _logger      = logger;
    }

    public async ValueTask<Result<Unit>> Handle(
        RequestPasswordResetCommand cmd, CancellationToken ct)
    {
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();

        // STEP 1 — Look up user by email (Dapper read)
        using var conn = _factory.CreateConnection();
        var userId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """SELECT "Id" FROM "Users" WHERE "Email" = @Email LIMIT 1""",
            new { Email = normalizedEmail });

        // STEP 2 — Anti-enumeration: if user not found, return 200 silently
        if (userId is null)
        {
            _logger.LogInformation(
                "PasswordReset requested for unknown email (no-op): {Email}", normalizedEmail);
            return Result<Unit>.Success(Unit.Value);
        }

        // STEP 3 — Generate 64-byte cryptographically random token
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        // STEP 4 — BCrypt hash (wf:6) to store in DB
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 6);

        // STEP 5 — Persist PasswordResetToken
        var expiresAt = DateTime.UtcNow.AddMinutes(_policy.ResetTokenExpiryMinutes);
        var token     = PasswordResetToken.Create(userId.Value, tokenHash, expiresAt);
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        // STEP 6 — Build reset link
        var frontendBaseUrl = _config["App:FrontendBaseUrl"] ?? "http://localhost:5173";
        var resetLink       = $"{frontendBaseUrl}/reset-password?token={rawToken}";

        // STEP 7 — Queue email (fire-and-forget via EmailChannel)
        await _emailSender.SendAsync(new EmailMessage(
            To:      normalizedEmail,
            Subject: "Reset your MyBudget password",
            Body:    $"<p>You requested a password reset for your MyBudget account.</p>" +
                     $"<p><a href=\"{resetLink}\">Click here to reset your password</a></p>" +
                     $"<p>This link expires in {_policy.ResetTokenExpiryMinutes} minutes.</p>" +
                     $"<p>If you did not request a password reset, you can safely ignore this email.</p>"),
            ct);

        _logger.LogInformation(
            "Password reset token created for user {UserId}", userId.Value);

        return Result<Unit>.Success(Unit.Value);
    }
}
