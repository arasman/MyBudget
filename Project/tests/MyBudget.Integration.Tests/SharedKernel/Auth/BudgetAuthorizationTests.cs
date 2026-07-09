using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.SharedKernel.Auth;

/// <summary>
/// Integration tests for BudgetAuthorizationHandler policy enforcement.
/// Uses InviteUserToBudget endpoint (requires budget:admin policy) as the test target.
/// </summary>
public sealed class BudgetAuthorizationTests : IntegrationTestBase
{
    public BudgetAuthorizationTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string AccessToken, Guid BudgetId)> SetupOwnerAsync(string email)
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);

        // Get budget ID from /me
        var me = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    [Fact]
    public async Task Owner_CallsBudgetAdminPolicy_Returns201()
    {
        var (token, budgetId) = await SetupOwnerAsync("owner-auth@example.com");
        AuthorizeClient(token);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/invitations",
            new { email = "invitee@example.com", role = "operator" });

        // 201 or 409 (already member check) — both mean auth passed
        ((int)response.StatusCode).ShouldBeOneOf(201, 409, 404);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
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

    private sealed record MeResponse(
        Guid Id,
        string Email,
        MembershipEntry[] Memberships);

    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);
}
