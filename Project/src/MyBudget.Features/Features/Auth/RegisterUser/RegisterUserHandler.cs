using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Email;
using MyBudget.Features.SharedKernel.Entities;
using RefreshTokenEntity = MyBudget.Features.SharedKernel.Entities.RefreshToken;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.Features.Auth.RegisterUser;

public sealed class RegisterUserHandler
    : IRequestHandler<RegisterUserCommand, Result<LoginResponse>>
{
    private readonly AppDbContext         _db;
    private readonly JwtTokenService      _jwt;
    private readonly IEmailSender         _emailSender;
    private readonly ISecurityAuditWriter _auditWriter;
    private readonly ILogger<RegisterUserHandler> _logger;

    public RegisterUserHandler(
        AppDbContext db,
        JwtTokenService jwt,
        IEmailSender emailSender,
        ISecurityAuditWriter auditWriter,
        ILogger<RegisterUserHandler> logger)
    {
        _db          = db;
        _jwt         = jwt;
        _emailSender = emailSender;
        _auditWriter = auditWriter;
        _logger      = logger;
    }

    public async ValueTask<Result<LoginResponse>> Handle(
        RegisterUserCommand cmd, CancellationToken ct)
    {
        // 1. Check for duplicate email (case-insensitive)
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(
            u => u.Email == normalizedEmail, ct);

        if (exists)
            return Result<LoginResponse>.Failure("AUTH_EMAIL_TAKEN");

        // 2. Hash password with BCrypt workFactor 12
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(cmd.Password, workFactor: 12);

        // 3. Create User
        var user = User.Create(
            cmd.Email,
            passwordHash,
            cmd.FirstName,
            cmd.LastName,
            string.IsNullOrEmpty(cmd.PreferredLocale) ? "en" : cmd.PreferredLocale);

        _db.Users.Add(user);

        // 4. Create default Budget + BudgetMembership (same transaction)
        var budget = Budget.Create($"{cmd.FirstName.Trim()}'s Budget", user.Id);
        _db.Budgets.Add(budget);

        var membership = BudgetMembership.Create(budget.Id, user.Id, BudgetRole.Owner);
        _db.BudgetMemberships.Add(membership);

        await _db.SaveChangesAsync(ct);

        // 5. Generate JWT pair
        var accessToken  = _jwt.GenerateAccessToken(user);
        var rawRefresh   = _jwt.GenerateRefreshToken();

        // 6. Hash refresh token (workFactor 6 — less critical than password, but still hashed)
        var refreshHash  = BCrypt.Net.BCrypt.HashPassword(rawRefresh, workFactor: 6);
        var refreshToken = RefreshTokenEntity.Create(
            user.Id,
            refreshHash,
            DateTime.UtcNow.AddDays(7));

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        await _auditWriter.WriteAsync(
            "AccountRegistered",
            userId: user.Id,
            email:  user.Email,
            ct:     ct);

        // 7. Send welcome email (fire-and-forget via IEmailSender channel)
        await _emailSender.SendAsync(new EmailMessage(
            To:      user.Email,
            Subject: "Welcome to MyBudget",
            Body:    $"<h1>Welcome {user.FirstName}!</h1><p>Your account is ready.</p>"), ct);

        _logger.LogInformation("User registered: {UserId}", user.Id);

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
}
