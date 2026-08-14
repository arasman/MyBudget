using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for POST /api/budgets/{id}/invitations.</summary>
public sealed class InviteUserToBudgetTests : IntegrationTestBase
{
    public InviteUserToBudgetTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupAdminAsync(string email = "admin-inv@example.com")
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me       = await Client.GetAsync("/api/auth/me");
        var meBody   = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    [Fact]
    public async Task AdminCaller_ValidInvite_Returns201()
    {
        var (token, budgetId) = await SetupAdminAsync();
        AuthorizeClient(token);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "invitee1@example.com", role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InviteResponse>(JsonOpts);
        body!.InvitationId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task RoleOwner_Returns422_CannotInviteAsOwner()
    {
        var (token, budgetId) = await SetupAdminAsync("admin-inv2@example.com");
        AuthorizeClient(token);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "invitee2@example.com", role = "owner" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AlreadyMember_Returns409()
    {
        var (token, budgetId) = await SetupAdminAsync("admin-inv3@example.com");
        AuthorizeClient(token);

        // Invite the owner themselves (already a member)
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "admin-inv3@example.com", role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownBudget_Returns404()
    {
        var login = await RegisterUserAsync("admin-inv4@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/invitations",
            new { email = "x@example.com", role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/invitations",
            new { email = "x@example.com", role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OperatorCaller_Returns403()
    {
        // Arrange: set up an admin who owns the budget
        var (_, budgetId) = await SetupAdminAsync("admin-inv5@example.com");

        // Register the operator user
        var operatorLogin = await RegisterUserAsync("operator-inv5@example.com");

        // Seed a BudgetMembership directly so the operator-role user is a member
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = BudgetMembership.Create(budgetId, operatorLogin.User.Id, BudgetRole.Operator);
        db.BudgetMemberships.Add(membership);
        await db.SaveChangesAsync();

        // Act: operator tries to invite someone — requires budget:admin (Admin or Owner role)
        AuthorizeClient(operatorLogin.AccessToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "target-inv5@example.com", role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadOnlyRoleHyphenated_StillSucceeds()
    {
        // Approval test for the TryParseRole → BudgetRoleStrings.TryParse delegation refactor:
        // captures current behavior (accepts "read-only") so the refactor cannot silently drop it.
        var (token, budgetId) = await SetupAdminAsync("admin-inv6@example.com");
        AuthorizeClient(token);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "invitee6@example.com", role = "read-only" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InviteResponse>(JsonOpts);
        body!.InvitationId.ShouldNotBe(Guid.Empty);
    }

    private sealed record InviteResponse(Guid InvitationId, DateTime ExpiresAt);
    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);
}
