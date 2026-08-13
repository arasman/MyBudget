using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for GET /api/budgets/{id}/members (MEMBERS-LIST-1, WU1 scope).</summary>
public sealed class ListBudgetMembersTests : IntegrationTestBase
{
    public ListBudgetMembersTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupOwnerAsync(string email)
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    private async Task AddMemberAsync(Guid budgetId, Guid userId, BudgetRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, userId, role));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Admin_ListsActiveMembers_Returns200_WithAllRows()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("list-members-owner@example.com");

        var admin    = await RegisterUserAsync("list-members-admin@example.com");
        var operatorUser = await RegisterUserAsync("list-members-operator@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddMemberAsync(budgetId, operatorUser.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/members");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListMembersResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Members.Count.ShouldBe(3);

        var ownerRow = body.Members.Single(m => m.Role == "owner");
        ownerRow.Email.ShouldBe("list-members-owner@example.com");

        var adminRow = body.Members.Single(m => m.Role == "admin");
        adminRow.UserId.ShouldBe(admin.User.Id);
        adminRow.FirstName.ShouldBe(admin.User.FirstName);
        adminRow.LastName.ShouldBe(admin.User.LastName);

        var operatorRow = body.Members.Single(m => m.Role == "operator");
        operatorRow.UserId.ShouldBe(operatorUser.User.Id);
    }

    [Fact]
    public async Task OperatorRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("list-members-owner2@example.com");

        var operatorLogin = await RegisterUserAsync("list-members-operator2@example.com");
        await AddMemberAsync(budgetId, operatorLogin.User.Id, BudgetRole.Operator);

        AuthorizeClient(operatorLogin.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/members");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);

    private sealed record ListMembersResponse(List<MemberRow> Members);
    private sealed record MemberRow(
        Guid   UserId,
        string Email,
        string FirstName,
        string LastName,
        string Role,
        DateTimeOffset JoinedAt);
}
