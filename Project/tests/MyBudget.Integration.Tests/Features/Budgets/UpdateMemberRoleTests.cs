using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for PATCH /api/budgets/{id}/members/{userId}/role (MEMBERS-ROLE-1).</summary>
public sealed class UpdateMemberRoleTests : IntegrationTestBase
{
    public UpdateMemberRoleTests(IntegrationTestFactory factory) : base(factory) { }

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

    private async Task<string> RoleOfAsync(string ownerToken, Guid budgetId, Guid userId)
    {
        AuthorizeClient(ownerToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/members");
        var body = await response.Content.ReadFromJsonAsync<ListMembersResponse>(JsonOpts);
        return body!.Members.Single(m => m.UserId == userId).Role;
    }

    [Fact]
    public async Task Owner_PromotesOperatorToAdmin_Returns200_RoleUpdated()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner1@example.com");
        var target = await RegisterUserAsync("role-target1@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "admin" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateRoleResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.Role.ShouldBe("admin");
        (await RoleOfAsync(ownerToken, budgetId, target.User.Id)).ShouldBe("admin");
    }

    [Fact]
    public async Task Owner_DemotesAdminToOperator_Returns200()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner2@example.com");
        var target = await RegisterUserAsync("role-target2@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Admin);

        AuthorizeClient(ownerToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RoleOfAsync(ownerToken, budgetId, target.User.Id)).ShouldBe("operator");
    }

    [Fact]
    public async Task Admin_ChangesReadOnlyMembersRole_Returns200()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner3@example.com");
        var admin  = await RegisterUserAsync("role-admin3@example.com");
        var target = await RegisterUserAsync("role-target3@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.ReadOnly);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RoleOfAsync(ownerToken, budgetId, target.User.Id)).ShouldBe("operator");
    }

    [Fact]
    public async Task Admin_TargetsAnotherAdmin_Returns403_CannotActOnAdmin_NoChangePersisted()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner4@example.com");
        var admin  = await RegisterUserAsync("role-admin4@example.com");
        var target = await RegisterUserAsync("role-target4@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_ADMIN");
        (await RoleOfAsync(ownerToken, budgetId, target.User.Id)).ShouldBe("admin");
    }

    [Fact]
    public async Task Caller_TargetsOwnUserId_Returns403_CannotActOnSelf_RoleUnchanged()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner5@example.com");
        var admin = await RegisterUserAsync("role-admin5@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{admin.User.Id}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_SELF");
        (await RoleOfAsync(ownerToken, budgetId, admin.User.Id)).ShouldBe("admin");
    }

    [Fact]
    public async Task Target_IsOwnerRow_Returns403_CannotActOnOwner_OwnerRoleUnchanged()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner6@example.com");
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var ownerId = meBody!.Id;

        var admin = await RegisterUserAsync("role-admin6@example.com");
        await AddMemberAsync(budgetId, admin.User.Id, BudgetRole.Admin);

        AuthorizeClient(admin.AccessToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{ownerId}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_ACT_ON_OWNER");
        (await RoleOfAsync(ownerToken, budgetId, ownerId)).ShouldBe("owner");
    }

    [Fact]
    public async Task PromoteToOwner_OnNonOwnerTarget_Returns422_CannotPromoteToOwner()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner7@example.com");
        var target = await RegisterUserAsync("role-target7@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        AuthorizeClient(ownerToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "owner" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOpts);
        problem!.Detail.ShouldBe("MEMBERS_CANNOT_PROMOTE_TO_OWNER");
    }

    [Fact]
    public async Task UnknownUserId_Returns404_MembersNotFound()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner8@example.com");

        AuthorizeClient(ownerToken);
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{Guid.NewGuid()}/role", new { role = "operator" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RoleChange_EvictsCache_SecondRequestByAffectedUserReflectsNewRole()
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync("role-owner9@example.com");
        var target = await RegisterUserAsync("role-target9@example.com");
        await AddMemberAsync(budgetId, target.User.Id, BudgetRole.Operator);

        // Warm the auth cache with the target's OLD role (Operator) via a budget:read-gated call.
        AuthorizeClient(target.AccessToken);
        var warmup = await Client.GetAsync($"/api/budgets/{budgetId}/cycles");
        warmup.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Owner promotes the target to Admin.
        AuthorizeClient(ownerToken);
        var promote = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/members/{target.User.Id}/role", new { role = "admin" });
        promote.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Immediately after, the target calls a budget:admin-gated endpoint. If the cache still
        // held the stale Operator role, this would be 403; a fresh Admin role must be 200.
        AuthorizeClient(target.AccessToken);
        var afterPromote = await Client.GetAsync($"/api/budgets/{budgetId}/members");
        afterPromote.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
    private sealed record ListMembersResponse(List<MemberRow> Members);
    private sealed record MemberRow(Guid UserId, string Email, string FirstName, string LastName, string Role, DateTimeOffset JoinedAt);
    private sealed record UpdateRoleResponse(Guid UserId, string Role);
    private sealed record ProblemResponse(string? Detail);
}
