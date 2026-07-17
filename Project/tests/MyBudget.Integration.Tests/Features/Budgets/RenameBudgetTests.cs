using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for PUT /api/budgets/{id}.</summary>
public sealed class RenameBudgetTests : IntegrationTestBase
{
    public RenameBudgetTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupOwnerAsync(string email = "rename-owner@example.com")
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    [Fact]
    public async Task HappyPath_AdminRole_Returns200_WithUpdatedName()
    {
        var (token, budgetId) = await SetupOwnerAsync();
        AuthorizeClient(token);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "Personal" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RenameBudgetResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Name.ShouldBe("Personal");
    }

    [Fact]
    public async Task OperatorRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("rename-owner2@example.com");

        var operatorLogin = await RegisterUserAsync("rename-operator@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, operatorLogin.User.Id, BudgetRole.Operator));
            await db.SaveChangesAsync();
        }

        AuthorizeClient(operatorLogin.AccessToken);
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "New Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BudgetNotFound_Returns404()
    {
        var login = await RegisterUserAsync("rename-notfound@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}", new { name = "New Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidName_Returns422()
    {
        var (token, budgetId) = await SetupOwnerAsync("rename-invalid@example.com");
        AuthorizeClient(token);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeletedBudget_Returns404()
    {
        var (token, budgetId) = await SetupOwnerAsync("rename-deleted@example.com");
        AuthorizeClient(token);

        // Soft-delete the budget
        await Client.DeleteAsync($"/api/budgets/{budgetId}");

        // BudgetAuthorizationHandler intercepts at policy layer and returns 404 for deleted budgets
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "New Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rename_CacheEvicted_SecondRenameReflectsNewName()
    {
        var (token, budgetId) = await SetupOwnerAsync("rename-cache@example.com");
        AuthorizeClient(token);

        var first = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "First Name" });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second rename must also succeed — proves the cache was evicted and did not hard-lock the first name
        var second = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}", new { name = "Second Name" });
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<RenameBudgetResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Name.ShouldBe("Second Name");
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
    private sealed record RenameBudgetResponse(Guid Id, string Name);
}
