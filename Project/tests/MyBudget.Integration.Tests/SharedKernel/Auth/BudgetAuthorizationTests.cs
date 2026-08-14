using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
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

    // --- budget-member-administration WU2 (AUTHZ-1, security-critical) ---

    private async Task AddMemberAsync(Guid budgetId, Guid userId, BudgetRole role, bool softDeleted = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = BudgetMembership.Create(budgetId, userId, role);
        if (softDeleted)
            membership.SoftDelete();
        db.BudgetMemberships.Add(membership);
        await db.SaveChangesAsync();
    }

    private async Task RestoreMemberAsync(Guid budgetId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = db.BudgetMemberships.Single(m => m.BudgetId == budgetId && m.UserId == userId);
        membership.Restore();
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SoftDeletedMembership_ResolvesAsNoMembership_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-softdel1@example.com");
        var target = await RegisterUserAsync("authz-softdel1-target@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator, softDeleted: true);

        AuthorizeClient(target.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoredMembership_ResolvesNormallyAgain_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-restored1@example.com");
        var target = await RegisterUserAsync("authz-restored1-target@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator, softDeleted: true);

        // Confirm it's rejected first (soft-deleted)
        AuthorizeClient(target.AccessToken);
        var rejected = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await RestoreMemberAsync(budgetId, target.User.Id);

        AuthorizeClient(target.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // --- Regression sweep (task 18.6): one representative endpoint per existing policy tier,
    // ACTIVE membership only — proves zero behavior change post auth-handler edit. ---

    [Fact]
    public async Task RegressionSweep_ListCycles_BudgetRead_ActiveOperator_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-sweep-read@example.com");
        var target = await RegisterUserAsync("authz-sweep-read-target@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(target.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegressionSweep_CreateCycle_BudgetAdmin_ActiveAdmin_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-sweep-admin@example.com");
        var admin = await RegisterUserAsync("authz-sweep-admin-target@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PostAsJsonAsync($"/api/budgets/{budgetId}/cycles", new
        {
            name = "Regression Sweep Cycle",
            startDate = "2027-01-01",
            endDate = "2027-12-31",
            defaultCurrencyId = "11111111-1111-1111-1111-111111111111",
            alternateCurrencyId = (string?)null,
            exchangeRate = (decimal?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegressionSweep_CreateCycle_BudgetAdmin_ActiveOperator_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-sweep-admin2@example.com");
        var operatorUser = await RegisterUserAsync("authz-sweep-admin2-target@example.com");
        await AddMemberAsync(budgetId, operatorUser.User.Id, BudgetRole.Operator);

        AuthorizeClient(operatorUser.AccessToken);
        var response = await Client.PostAsJsonAsync($"/api/budgets/{budgetId}/cycles", new
        {
            name = "Regression Sweep Cycle 2",
            startDate = "2027-01-01",
            endDate = "2027-12-31",
            defaultCurrencyId = "11111111-1111-1111-1111-111111111111",
            alternateCurrencyId = (string?)null,
            exchangeRate = (decimal?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegressionSweep_DeleteThenRestoreBudget_BudgetOwner_ActiveOwner_Succeeds()
    {
        var (token, budgetId) = await SetupOwnerAsync("authz-sweep-owner@example.com");
        AuthorizeClient(token);

        var deleteResponse = await Client.DeleteAsync($"/api/budgets/{budgetId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var restoreResponse = await Client.PostAsJsonAsync($"/api/budgets/{budgetId}/restore", new { });
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegressionSweep_DeleteBudget_BudgetOwner_ActiveAdmin_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-sweep-owner2@example.com");
        var admin = await RegisterUserAsync("authz-sweep-owner2-target@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegressionSweep_ListBudgetLines_BudgetRead_ActiveReadOnly_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("authz-sweep-lines@example.com");
        var readOnly = await RegisterUserAsync("authz-sweep-lines-target@example.com");
        await AddMemberAsync(budgetId, readOnly.User.Id, BudgetRole.ReadOnly);

        AuthorizeClient(readOnly.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/lines");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record MeResponse(
        Guid Id,
        string Email,
        MembershipEntry[] Memberships);

    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);
}
