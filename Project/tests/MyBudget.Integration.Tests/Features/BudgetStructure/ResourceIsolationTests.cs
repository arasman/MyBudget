using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for resource isolation.
/// Covers REQ-SC-03: cross-budget access returns 404.
/// </summary>
public sealed class ResourceIsolationTests : BudgetStructureTestBase
{
    public ResourceIsolationTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task CrossBudget_AccessCycle_Returns404()
    {
        // Owner A creates a cycle
        var (tokenA, budgetAId) = await SetupOwnerAsync("iso-cycle-a@example.com");
        var cycleId             = await CreateCycleAsync(budgetAId);

        // Owner B registers and tries to access Owner A's cycle under B's budget
        var loginB  = await RegisterUserAsync("iso-cycle-b@example.com");
        AuthorizeClient(loginB.AccessToken);
        var me      = await Client.GetAsync("/api/auth/me");
        var meBody  = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetBId = meBody!.Memberships[0].BudgetId;

        var response = await Client.GetAsync($"/api/budgets/{budgetBId}/cycles/{cycleId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossBudget_AccessPeriodLines_Returns404()
    {
        var (tokenA, budgetAId) = await SetupOwnerAsync("iso-period-a@example.com");
        var cycleId             = await CreateCycleAsync(budgetAId);
        var periodId            = await CreatePeriodAsync(budgetAId, cycleId);

        // B tries to list budget lines for A's period under B's budget
        var loginB  = await RegisterUserAsync("iso-period-b@example.com");
        AuthorizeClient(loginB.AccessToken);
        var me      = await Client.GetAsync("/api/auth/me");
        var meBody  = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetBId = meBody!.Memberships[0].BudgetId;

        var response = await Client.GetAsync($"/api/budgets/{budgetBId}/periods/{periodId}/lines");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossBudget_UpdateCycle_Returns404()
    {
        var (tokenA, budgetAId) = await SetupOwnerAsync("iso-upd-a@example.com");
        var cycleId             = await CreateCycleAsync(budgetAId);

        var loginB  = await RegisterUserAsync("iso-upd-b@example.com");
        AuthorizeClient(loginB.AccessToken);
        var me      = await Client.GetAsync("/api/auth/me");
        var meBody  = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetBId = meBody!.Memberships[0].BudgetId;

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetBId}/cycles/{cycleId}",
            new { name = "Hacked", startDate = new DateOnly(2025, 1, 1), endDate = new DateOnly(2025, 12, 31) });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossBudget_DeleteCycleGroup_Returns404()
    {
        var (tokenA, budgetAId) = await SetupOwnerAsync("iso-del-a@example.com");
        var groupId             = await CreateCategoryGroupAsync(budgetAId);

        var loginB  = await RegisterUserAsync("iso-del-b@example.com");
        AuthorizeClient(loginB.AccessToken);
        var me      = await Client.GetAsync("/api/auth/me");
        var meBody  = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        var budgetBId = meBody!.Memberships[0].BudgetId;

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetBId}/category-groups/{groupId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

}
