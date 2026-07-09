using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Entities;
using RefreshTokenEntity = MyBudget.Features.SharedKernel.Entities.RefreshToken;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.LoginUser;

public sealed class LoginUserHandler
    : IRequestHandler<LoginUserCommand, Result<LoginResponse>>
{
    private readonly AppDbContext    _db;
    private readonly ConnectionFactory _factory;
    private readonly JwtTokenService _jwt;
    private readonly ILogger<LoginUserHandler> _logger;

    public LoginUserHandler(
        AppDbContext db,
        ConnectionFactory factory,
        JwtTokenService jwt,
        ILogger<LoginUserHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _jwt     = jwt;
        _logger  = logger;
    }

    public async ValueTask<Result<LoginResponse>> Handle(
        LoginUserCommand cmd, CancellationToken ct)
    {
        // 1. Dapper SELECT user by email (case-insensitive)
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        using var conn = _factory.CreateConnection();

        var row = await conn.QuerySingleOrDefaultAsync<UserRow>(
            """
            SELECT "Id", "Email", "PasswordHash", "FirstName", "LastName", "PreferredLocale"
            FROM "Users"
            WHERE "Email" = @Email
            LIMIT 1
            """,
            new { Email = normalizedEmail });

        // 2. BCrypt.Verify — same response for unknown email and wrong password (no enumeration)
        if (row is null || !BCrypt.Net.BCrypt.Verify(cmd.Password, row.PasswordHash))
            return Result<LoginResponse>.Failure("AUTH_INVALID_CREDENTIALS");

        // 3. Update LastLoginAt via EF
        var user = await _db.Users.FindAsync([row.Id], ct)
                   ?? throw new InvalidOperationException("User not found after login check.");
        user.UpdateLastLogin();
        await _db.SaveChangesAsync(ct);

        // 4. Generate JWT pair
        var accessToken = _jwt.GenerateAccessToken(user);
        var rawRefresh  = _jwt.GenerateRefreshToken();

        // 5. BCrypt-hash refresh token and persist
        var refreshHash  = BCrypt.Net.BCrypt.HashPassword(rawRefresh, workFactor: 6);
        var refreshToken = RefreshTokenEntity.Create(user.Id, refreshHash, DateTime.UtcNow.AddDays(7));
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

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

    // Lightweight Dapper projection — no navigation properties
    private sealed record UserRow(
        Guid   Id,
        string Email,
        string PasswordHash,
        string FirstName,
        string LastName,
        string PreferredLocale);
}
