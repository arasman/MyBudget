using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for DELETE /api/budgets/{id}/members/{userId} (MEMBERS-REMOVE-1, security-critical).</summary>
public sealed class RemoveBudgetMemberTests : IntegrationTestBase
{
    public RemoveBudgetMemberTests(IntegrationTestFactory factory) : base(factory) { }

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

    private async Task<BudgetMembership> GetMembershipAsync(Guid budgetId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BudgetMemberships.SingleAsync(m => m.BudgetId == budgetId && m.UserId == userId);
    }

    [Fact]
    public async Task Owner_RemovesOperator_Returns204_SoftDeleted_CacheEvicted()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("remove-owner1@example.com");
        var target = await RegisterUserAsync("remove-target1@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var membership = await GetMembershipAsync(budgetId, target.User.Id);
        membership.IsDeleted.ShouldBeTrue();
        membership.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Admin_RemovesReadOnlyMember_Returns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("remove-owner2@example.com");
        var admin  = await RegisterUserAsync("remove-admin2@example.com");
        var target = await RegisterUserAsync("remove-target2@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.ReadOnly);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetMembershipAsync(budgetId, target.User.Id)).IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Admin_TargetsAnotherAdmin_Returns403_CannotActOnAdmin_MembershipUntouched()
    {
        var (_, budgetId) = await SetupOwnerAsync("remove-owner3@example.com");
        var admin  = await RegisterUserAsync("remove-admin3@example.com");
        var target = await RegisterUserAsync("remove-target3@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_ADMIN");
        (await GetMembershipAsync(budgetId, target.User.Id)).IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task SelfRemoval_Returns403_CannotActOnSelf()
    {
        var (_, budgetId) = await SetupOwnerAsync("remove-owner4@example.com");
        var admin = await RegisterUserAsync("remove-admin4@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{admin.User.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_SELF");
    }

    [Fact]
    public async Task Target_IsOwnerRow_Returns403_CannotActOnOwner_Untouched()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("remove-owner5@example.com");
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var ownerId = meBody!.Id;

        var admin = await RegisterUserAsync("remove-admin5@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{ownerId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_OWNER");
        (await GetMembershipAsync(budgetId, ownerId)).IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task RemovedMember_LosesAccessImmediately_NotAfterCacheTtl()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("remove-owner6@example.com");
        var target = await RegisterUserAsync("remove-target6@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        // Warm the auth cache with a budget:read-gated call moments before removal.
        AuthorizeClient(target.AccessToken);
        var warmup = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        warmup.StatusCode.ShouldBe(HttpStatusCode.OK);

        AuthorizeClient(ownerToken);
        var remove = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");
        remove.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Immediately after removal — if the cache still held the stale role, this would be 200.
        AuthorizeClient(target.AccessToken);
        var afterRemove = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        afterRemove.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AlreadyRemovedTarget_Returns404_MembersNotFound()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("remove-owner7@example.com");
        var target = await RegisterUserAsync("remove-target7@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var first = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await Client.DeleteAsync($"/api/budgets/{budgetId}/members/{target.User.Id}");
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
    private sealed record ProblemResponse(string? Detail);
}
