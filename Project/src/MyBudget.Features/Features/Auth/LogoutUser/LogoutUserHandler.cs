using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.LogoutUser;

public sealed class LogoutUserHandler
    : IRequestHandler<LogoutUserCommand, Result<bool>>
{
    private readonly AppDbContext         _db;
    private readonly ConnectionFactory    _factory;
    private readonly ISecurityAuditWriter _auditWriter;
    private readonly ILogger<LogoutUserHandler> _logger;

    public LogoutUserHandler(
        AppDbContext db,
        ConnectionFactory factory,
        ISecurityAuditWriter auditWriter,
        ILogger<LogoutUserHandler> logger)
    {
        _db          = db;
        _factory     = factory;
        _auditWriter = auditWriter;
        _logger      = logger;
    }

    public async ValueTask<Result<bool>> Handle(
        LogoutUserCommand cmd, CancellationToken ct)
    {
        // 1. Fetch active tokens for user via Dapper
        using var conn = _factory.CreateConnection();

        var candidates = (await conn.QueryAsync<TokenRow>(
            """
            SELECT "Id", "TokenHash"
            FROM "RefreshTokens"
            WHERE "UserId" = @UserId AND "RevokedAt" IS NULL AND "ExpiresAt" > NOW()
            """,
            new { UserId = cmd.UserId })).ToList();

        // 2. Find matching token
        TokenRow? matched = null;
        foreach (var c in candidates)
        {
            if (BCrypt.Net.BCrypt.Verify(cmd.RefreshToken, c.TokenHash))
            {
                matched = c;
                break;
            }
        }

        // 3. Idempotent — if not found or already revoked, return 200 anyway
        if (matched is null)
            return Result<bool>.Success(true);

        // 4. Revoke via EF
        var token = await _db.RefreshTokens.FindAsync([matched.Id], ct);
        if (token is not null && !token.IsRevoked)
        {
            token.Revoke();
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("User {UserId} logged out", cmd.UserId);
        }

        await _auditWriter.WriteAsync(
            "TokenRevoked",
            userId: cmd.UserId,
            email:  null,
            ct:     ct);

        return Result<bool>.Success(true);
    }

    private sealed record TokenRow(Guid Id, string TokenHash);
}
