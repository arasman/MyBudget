using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Auth;

/// <summary>
/// Integration tests verifying that auth handlers write SecurityAuditLog entries.
/// Each test calls the real HTTP endpoint and asserts a row exists in SecurityAuditLogs.
/// </summary>
public sealed class SecurityAuditLogTests : IntegrationTestBase
{
    public SecurityAuditLogTests(IntegrationTestFactory factory) : base(factory) { }

    // -------------------------------------------------------------------------
    // 3.8 — POST /auth/login with valid credentials → SuccessfulLogin
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidLogin_WritesSuccessfulLoginEntry_WithUserIdAndEmail()
    {
        await RegisterUserAsync("audit-login-ok@example.com");
        // Clear audit rows produced by register so we only see the login event
        await ClearAuditLogsAsync();

        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "audit-login-ok@example.com",
            password = "Password1",
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "SuccessfulLogin");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldNotBeNull();
        entry.Email.ShouldBe("audit-login-ok@example.com");
    }

    // -------------------------------------------------------------------------
    // 3.9 — POST /auth/login with invalid credentials → FailedLogin
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvalidLogin_WritesFailedLoginEntry()
    {
        await RegisterUserAsync("audit-login-fail@example.com");
        await ClearAuditLogsAsync();

        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "audit-login-fail@example.com",
            password = "WrongPassword1",
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "FailedLogin");

        entry.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnknownEmailLogin_WritesFailedLoginEntry_WithNullUserId()
    {
        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "ghost-audit@example.com",
            password = "Password1",
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "FailedLogin");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldBeNull(); // user not found → no UserId
    }

    // -------------------------------------------------------------------------
    // 3.10 — POST /auth/refresh → TokenRefreshed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshToken_WritesTokenRefreshedEntry()
    {
        var login = await RegisterUserAsync("audit-refresh@example.com");
        await ClearAuditLogsAsync();

        await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login.RefreshToken,
            userId       = login.User.Id,
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "TokenRefreshed");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldBe(login.User.Id);
    }

    // -------------------------------------------------------------------------
    // 3.11 — POST /auth/logout → TokenRevoked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_WritesTokenRevokedEntry()
    {
        var login = await RegisterUserAsync("audit-logout@example.com");
        AuthorizeClient(login.AccessToken);
        await ClearAuditLogsAsync();

        await Client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = login.RefreshToken,
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "TokenRevoked");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldBe(login.User.Id);
    }

    // -------------------------------------------------------------------------
    // 3.12 — POST /auth/register → AccountRegistered
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_WritesAccountRegisteredEntry_WithUserIdAndEmail()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email           = "audit-register@example.com",
            password        = "Password1",
            firstName       = "Audit",
            lastName        = "User",
            preferredLocale = "en",
        });

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = db.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "AccountRegistered");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldNotBeNull();
        entry.Email.ShouldBe("audit-register@example.com");
    }

    // -------------------------------------------------------------------------
    // 3.13 — POST /auth/invitations/accept → InvitationAccepted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AcceptInvitation_WritesInvitationAcceptedEntry()
    {
        // Setup admin user + budget
        var admin = await RegisterUserAsync("audit-admin@example.com");
        AuthorizeClient(admin.AccessToken);

        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetId = meBody!.Memberships[0].BudgetId;

        // Register invitee
        var invitee = await RegisterUserAsync("audit-invitee@example.com");

        // Seed invitation with a known raw token directly in DB
        const string rawToken = "known-audit-test-token-12345678";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 4);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = Invitation.Create(
                budgetId:        budgetId,
                inviteeEmail:    "audit-invitee@example.com",
                role:            BudgetRole.Operator,
                tokenHash:       tokenHash,
                expiresAt:       DateTime.UtcNow.AddDays(7),
                invitedByUserId: admin.User.Id);
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync();
        }

        // Clear audit rows from setup
        await ClearAuditLogsAsync();

        // Accept invitation as invitee
        AuthorizeClient(invitee.AccessToken);
        await Client.PostAsJsonAsync("/api/auth/invitations/accept", new { token = rawToken });

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry    = verifyDb.SecurityAuditLogs
            .SingleOrDefault(e => e.Event == "InvitationAccepted");

        entry.ShouldNotBeNull();
        entry.UserId.ShouldBe(invitee.User.Id);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task ClearAuditLogsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SecurityAuditLogs.RemoveRange(db.SecurityAuditLogs);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);
}
