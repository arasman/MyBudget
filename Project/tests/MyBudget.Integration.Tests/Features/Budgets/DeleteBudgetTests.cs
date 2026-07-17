using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for DELETE /api/budgets/{id}.</summary>
public sealed class DeleteBudgetTests : IntegrationTestBase
{
    public DeleteBudgetTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupOwnerAsync(string email = "delete-owner@example.com")
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    [Fact]
    public async Task HappyPath_OwnerRole_Returns204()
    {
        var (token, budgetId) = await SetupOwnerAsync();
        AuthorizeClient(token);

        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminRole_NonOwner_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("delete-owner2@example.com");

        var adminLogin = await RegisterUserAsync("delete-admin@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, adminLogin.User.Id, BudgetRole.Admin));
            await db.SaveChangesAsync();
        }

        AuthorizeClient(adminLogin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AlreadyDeleted_Returns404()
    {
        var (token, budgetId) = await SetupOwnerAsync("delete-twice@example.com");
        AuthorizeClient(token);

        // First delete
        await Client.DeleteAsync($"/api/budgets/{budgetId}");

        // Second delete — budget is soft-deleted → auth handler returns 404
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BudgetNotFound_Returns404()
    {
        var login = await RegisterUserAsync("delete-notfound@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.DeleteAsync($"/api/budgets/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
}
