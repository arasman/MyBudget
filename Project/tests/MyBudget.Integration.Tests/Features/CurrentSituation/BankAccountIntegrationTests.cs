using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.CurrentSituation;

/// <summary>
/// Integration tests for BankAccount CRUD endpoints.
/// Covers spec BA-1 through BA-4.
/// </summary>
public sealed class BankAccountIntegrationTests : CurrentSituationTestBase
{
    public BankAccountIntegrationTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    // ── BA-1: Create BankAccount ──────────────────────────────────────────────

    [Fact]
    public async Task CreateBankAccount_ValidPayload_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-create1@example.com");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = "Caja GTQ", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateBankAccount_AliasExceedsLength_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-create2@example.com");
        var longAlias     = new string('A', 101);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = longAlias, isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateBankAccount_OperatorRole_Returns403()
    {
        var (_, budgetId)   = await SetupOwnerAsync("ba-create3-owner@example.com");
        var operatorToken   = await SetupOperatorAsync(budgetId, "ba-create3-op@example.com");
        AuthorizeClient(operatorToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = "Op Account", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── BA-2: List BankAccounts ───────────────────────────────────────────────

    [Fact]
    public async Task ListBankAccounts_ReturnsActiveAccountsOrderedByDisplayOrder()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list1@example.com");
        await CreateBankAccountAsync(budgetId, "Account C", displayOrder: 3);
        await CreateBankAccountAsync(budgetId, "Account A", displayOrder: 1);
        await CreateBankAccountAsync(budgetId, "Account B", displayOrder: 2);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.Length.ShouldBe(3);
        list[0].DisplayOrder.ShouldBe(1);
        list[1].DisplayOrder.ShouldBe(2);
        list[2].DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public async Task ListBankAccounts_SoftDeletedExcluded()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list2@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "Active");
        var deletedId     = await CreateBankAccountAsync(budgetId, "Deleted", displayOrder: 2);

        await DeleteBankAccountAsync(budgetId, deletedId);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.Length.ShouldBe(1);
        list[0].Id.ShouldBe(accountId);
    }

    [Fact]
    public async Task ListBankAccounts_ReadRole_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-list3-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "ba-list3-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── BA-3: Update BankAccount ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateBankAccount_PersistsAliasChange()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-update1@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "Original Alias");

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}",
            new { alias = "Updated Alias", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");
        var list = await listResponse.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.Single().Alias.ShouldBe("Updated Alias");
    }

    [Fact]
    public async Task UpdateBankAccount_DeletedAccount_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-update2@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "To Delete");

        await DeleteBankAccountAsync(budgetId, accountId);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}",
            new { alias = "Should Fail", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── BA-4: Soft-Delete BankAccount ─────────────────────────────────────────

    [Fact]
    public async Task DeleteBankAccount_SetsSoftDeleteAndReturns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-delete1@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "To Delete");

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify excluded from list
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/bank-accounts");
        var list = await listResponse.Content.ReadFromJsonAsync<BankAccountListItem[]>(JsonOpts);
        list!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteBankAccount_ExistingCutBankAccountRows_Unaffected()
    {
        // BA-4: soft-delete always allowed, even if referenced in historical cuts
        var (_, budgetId) = await SetupOwnerAsync("ba-delete2@example.com");
        var cutDate       = new DateOnly(2026, 7, 28);
        await SetupActiveCycleAndPeriodAsync(budgetId, cutDate);
        var accountId     = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        // Create a cut record referencing this account
        (await UpsertCutRecordAsync(budgetId, cutDate, 7.8m, [(accountId, 1000m)]))
            .EnsureSuccessStatusCode();

        // Soft-delete the account — should succeed even with CutBankAccount rows
        var deleteResp = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}");
        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The cut record should still return the account row (historical snapshot)
        var getResp = await GetCutRecordAsync(budgetId, cutDate);
        getResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await getResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeFalse();
        cut.Accounts.Count.ShouldBe(1);
        cut.Accounts[0].Alias.ShouldBe("Caja GTQ");
    }
}
