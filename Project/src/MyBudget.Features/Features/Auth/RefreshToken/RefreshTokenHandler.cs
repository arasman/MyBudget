using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Entities;
using RefreshTokenEntity = MyBudget.Features.SharedKernel.Entities.RefreshToken;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.RefreshToken;

public sealed class RefreshTokenHandler
    : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly JwtTokenService   _jwt;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        AppDbContext db,
        ConnectionFactory factory,
        JwtTokenService jwt,
        ILogger<RefreshTokenHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _jwt     = jwt;
        _logger  = logger;
    }

    public async ValueTask<Result<LoginResponse>> Handle(
        RefreshTokenCommand cmd, CancellationToken ct)
    {
        // 1. Fetch all refresh tokens for user (both active and revoked — need revoked for theft detection)
        using var conn = _factory.CreateConnection();

        var candidates = (await conn.QueryAsync<RefreshTokenRow>(
            """
            SELECT "Id", "TokenHash", "ExpiresAt", "RevokedAt", "ReplacedByTokenId"
            FROM "RefreshTokens"
            WHERE "UserId" = @UserId
            ORDER BY "CreatedAt" DESC
            """,
            new { UserId = cmd.UserId })).ToList();

        // 2. Find matching token via BCrypt.Verify
        RefreshTokenRow? matched = null;
        foreach (var candidate in candidates)
        {
            if (BCrypt.Net.BCrypt.Verify(cmd.RefreshToken, candidate.TokenHash))
            {
                matched = candidate;
                break;
            }
        }

        if (matched is null)
            return Result<LoginResponse>.Failure("AUTH_REFRESH_TOKEN_INVALID");

        // 3. Check expiry
        if (matched.ExpiresAt < DateTime.UtcNow)
            return Result<LoginResponse>.Failure("AUTH_REFRESH_TOKEN_EXPIRED");

        // 4. Reuse detection — token already revoked means theft
        if (matched.RevokedAt.HasValue)
        {
            // Revoke entire family
            await RevokeEntireFamilyAsync(cmd.UserId, ct);
            _logger.LogWarning("Refresh token reuse detected for user {UserId}", cmd.UserId);
            return Result<LoginResponse>.Failure("AUTH_REFRESH_TOKEN_REUSE");
        }

        // 5. Valid token — begin rotation
        var oldToken = await _db.RefreshTokens.FindAsync([matched.Id], ct)
                       ?? throw new InvalidOperationException("Token not found in EF context.");

        var rawRefresh  = _jwt.GenerateRefreshToken();
        var refreshHash = BCrypt.Net.BCrypt.HashPassword(rawRefresh, workFactor: 6);
        var newToken    = RefreshTokenEntity.Create(cmd.UserId, refreshHash, DateTime.UtcNow.AddDays(7));

        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);  // get newToken.Id

        oldToken.Revoke(newToken.Id);
        await _db.SaveChangesAsync(ct);

        // 6. Generate new access token
        var user = await _db.Users.FindAsync([cmd.UserId], ct)
                   ?? throw new InvalidOperationException("User not found.");
        var accessToken = _jwt.GenerateAccessToken(user);

        var response = new LoginResponse(
            AccessToken:  accessToken,
            RefreshToken: rawRefresh,
            ExpiresIn:    15 * 60,
            User: new UserProfile(
                user.Id, user.Email, user.FirstName, user.LastName, user.PreferredLocale));

        return Result<LoginResponse>.Success(response);
    }

    private async Task RevokeEntireFamilyAsync(Guid userId, CancellationToken ct)
    {
        var allTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        foreach (var t in allTokens)
            if (!t.IsRevoked)
                t.Revoke();

        await _db.SaveChangesAsync(ct);
    }

    private sealed record RefreshTokenRow(
        Guid      Id,
        string    TokenHash,
        DateTime  ExpiresAt,
        DateTime? RevokedAt,
        Guid?     ReplacedByTokenId);
}
