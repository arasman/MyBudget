using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for POST /api/budgets/{id}/restore.</summary>
public sealed class RestoreBudgetTests : IntegrationTestBase
{
    public RestoreBudgetTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupDeletedBudgetAsync(
        string email = "restore-owner@example.com")
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetId = meBody!.Memberships[0].BudgetId;

        // Soft-delete the budget via the API
        await Client.DeleteAsync($"/api/budgets/{budgetId}");

        return (login.AccessToken, budgetId);
    }

    [Fact]
    public async Task HappyPath_OwnerRestoresDeletedBudget_Returns200()
    {
        var (token, budgetId) = await SetupDeletedBudgetAsync();
        AuthorizeClient(token);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RestoreBudgetResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(budgetId);
    }

    [Fact]
    public async Task BudgetNotDeleted_Returns404()
    {
        var login = await RegisterUserAsync("restore-notdeleted@example.com");
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetId = meBody!.Memberships[0].BudgetId;

        // Budget is active (not deleted) — should return 404
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminRole_Returns404()
    {
        var (_, budgetId) = await SetupDeletedBudgetAsync("restore-owner2@example.com");

        var adminLogin = await RegisterUserAsync("restore-admin@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, adminLogin.User.Id, BudgetRole.Admin));
            await db.SaveChangesAsync();
        }

        AuthorizeClient(adminLogin.AccessToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/restore", new { });

        // Non-owner members must not be able to confirm that a deleted budget exists.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var (_, budgetId) = await SetupDeletedBudgetAsync("restore-owner3@example.com");

        // Completely unrelated user — no membership in this budget
        var nonMemberLogin = await RegisterUserAsync("restore-nonmember@example.com");
        AuthorizeClient(nonMemberLogin.AccessToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentBudget_Returns404()
    {
        var login = await RegisterUserAsync("restore-nonexistent@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
    private sealed record RestoreBudgetResponse(Guid Id, string Name);
}
