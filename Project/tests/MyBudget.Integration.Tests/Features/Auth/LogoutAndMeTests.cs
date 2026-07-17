using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Integration.Tests.Features.Auth;

/// <summary>Integration tests for POST /api/auth/logout and GET /api/auth/me.</summary>
public sealed class LogoutAndMeTests : IntegrationTestBase
{
    public LogoutAndMeTests(IntegrationTestFactory factory) : base(factory) { }

    // --- LogoutUser ---

    [Fact]
    public async Task AuthenticatedLogout_Returns200_AndRevokesToken()
    {
        var login = await RegisterUserAsync("logout@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = login.RefreshToken,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = db.RefreshTokens.Single(t => t.UserId == login.User.Id);
        token.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SecondLogout_SameToken_Returns200_Idempotent()
    {
        var login = await RegisterUserAsync("logout2@example.com");
        AuthorizeClient(login.AccessToken);

        await Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });
        var second = await Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnauthenticatedLogout_Returns401()
    {
        // No Authorization header
        Client.DefaultRequestHeaders.Remove("Authorization");
        var response = await Client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = "any",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- GetCurrentUser ---

    [Fact]
    public async Task AuthenticatedMe_Returns200_WithUserProfile()
    {
        var login = await RegisterUserAsync("me@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Email.ShouldBe("me@example.com");
        body.Memberships.ShouldNotBeEmpty(); // default budget membership
    }

    [Fact]
    public async Task NoAuthHeader_Me_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");
        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- GetCurrentUser IsDeleted field ---

    [Fact]
    public async Task Me_ActiveMembership_ReturnsIsDeletedFalse()
    {
        var login = await RegisterUserAsync("me-isdeleted-active@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Memberships.ShouldNotBeEmpty();
        body.Memberships[0].IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Me_SoftDeletedBudget_IncludesMembershipWithIsDeletedTrue()
    {
        var login = await RegisterUserAsync("me-isdeleted-deleted@example.com");
        AuthorizeClient(login.AccessToken);

        // Get the auto-created budget id
        var meInitial = await Client.GetAsync("/api/auth/me");
        var meBody    = await meInitial.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetId  = meBody!.Memberships[0].BudgetId;

        // Soft-delete the budget directly via the DB to bypass auth handler 404
        using (var scope = Factory.Services.CreateScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var budget = await db.Budgets.FindAsync(budgetId);
            budget!.SoftDelete();
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        body.ShouldNotBeNull();
        // Deleted membership MUST be included (not filtered)
        body.Memberships.ShouldContain(m => m.BudgetId == budgetId && m.IsDeleted == true);
    }

    private sealed record MeResponse(
        Guid   Id,
        string Email,
        string FirstName,
        string LastName,
        MembershipEntry[] Memberships);

    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
}
