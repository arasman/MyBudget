using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Features.CurrentSituation;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BankAccounts;

/// <summary>
/// Integration tests for GET .../bank-accounts?includeDeleted=true.
/// Covers spec BA-2 (amended).
/// </summary>
public sealed class ListBankAccountsIncludeDeletedTests : CurrentSituationTestBase
{
    public ListBankAccountsIncludeDeletedTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    [Fact]
    public async Task GetWithoutParam_ExcludesDeletedAccounts()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list-del1@example.com");
        var activeId      = await CreateBankAccountAsync(budgetId, "Active Account", displayOrder: 1);
        var deletedId     = await CreateBankAccountAsync(budgetId, "Deleted Account", displayOrder: 2);
        await DeleteBankAccountAsync(budgetId, deletedId);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.Length.ShouldBe(1);
        list[0].Id.ShouldBe(activeId);
        list[0].DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetWithIncludeDeletedTrue_ReturnsBothAccounts()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list-del2@example.com");
        var activeId      = await CreateBankAccountAsync(budgetId, "Active", displayOrder: 1);
        var deletedId     = await CreateBankAccountAsync(budgetId, "Deleted", displayOrder: 2);
        await DeleteBankAccountAsync(budgetId, deletedId);

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/bank-accounts?includeDeleted=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.Length.ShouldBe(2);

        var active  = list.Single(a => a.Id == activeId);
        var deleted = list.Single(a => a.Id == deletedId);

        active.DeletedAt.ShouldBeNull();
        deleted.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeletedAt_FieldAlwaysPresent_OnAllItems()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list-del3@example.com");
        await CreateBankAccountAsync(budgetId, "Account 1");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.ShouldNotBeEmpty();
        list.ShouldAllBe(a => a.DeletedAt == null); // active accounts have null deletedAt
    }
}
