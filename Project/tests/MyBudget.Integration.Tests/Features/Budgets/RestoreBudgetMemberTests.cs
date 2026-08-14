using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for POST /api/budgets/{id}/members/{userId}/restore (MEMBERS-RESTORE-1, security-critical).</summary>
public sealed class RestoreBudgetMemberTests : IntegrationTestBase
{
    public RestoreBudgetMemberTests(IntegrationTestFactory factory) : base(factory) { }

    private async Task<(string Token, Guid BudgetId)> SetupOwnerAsync(string email)
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    private async Task<Guid> AddSoftDeletedMemberAsync(Guid budgetId, Guid userId, BudgetRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = BudgetMembership.Create(budgetId, userId, role);
        membership.SoftDelete();
        db.BudgetMemberships.Add(membership);
        await db.SaveChangesAsync();
        return membership.Id;
    }

    private async Task AddMemberAsync(Guid budgetId, Guid userId, BudgetRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BudgetMemberships.Add(BudgetMembership.Create(budgetId, userId, role));
        await db.SaveChangesAsync();
    }

    private async Task<BudgetMembership> GetMembershipAsync(Guid budgetId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BudgetMemberships.SingleAsync(m => m.BudgetId == budgetId && m.UserId == userId);
    }

    [Fact]
    public async Task Owner_RestoresSoftDeletedOperator_Returns200_RoleUnchanged_CacheEvicted()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("restoremem-owner1@example.com");
        var target = await RegisterUserAsync("restoremem-target1@example.com");
        await AddSoftDeletedMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RestoreResponse>(JsonOpts);
        body!.Role.ShouldBe("operator");

        var membership = await GetMembershipAsync(budgetId, target.User.Id);
        membership.IsDeleted.ShouldBeFalse();
        membership.DeletedAt.ShouldBeNull();
        membership.Role.ShouldBe(BudgetRole.Operator);
    }

    [Fact]
    public async Task Admin_RestoresSoftDeletedReadOnlyMember_Returns200_RoleUnchanged()
    {
        var (_, budgetId) = await SetupOwnerAsync("restoremem-owner2@example.com");
        var admin  = await RegisterUserAsync("restoremem-admin2@example.com");
        var target = await RegisterUserAsync("restoremem-target2@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddSoftDeletedMemberAsync(budgetId, target.User.Id, BudgetRole.ReadOnly);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RestoreResponse>(JsonOpts);
        body!.Role.ShouldBe("read-only");
    }

    [Fact]
    public async Task Admin_RestoringSoftDeletedAdmin_Returns403_CannotActOnAdmin()
    {
        var (_, budgetId) = await SetupOwnerAsync("restoremem-owner3@example.com");
        var admin  = await RegisterUserAsync("restoremem-admin3@example.com");
        var target = await RegisterUserAsync("restoremem-target3@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddSoftDeletedMemberAsync(budgetId, target.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_ADMIN");
    }

    [Fact]
    public async Task RestoringAlreadyActiveMembership_Returns409_MembersNotDeleted()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("restoremem-owner4@example.com");
        var target = await RegisterUserAsync("restoremem-target4@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_NOT_DELETED");
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
    private sealed record RestoreResponse(Guid UserId, string Role);
    private sealed record ProblemResponse(string? Detail);
}
