using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Features.CurrentSituation;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BankAccounts;

/// <summary>
/// Integration tests for alias uniqueness enforcement (BA-1 + BA-3 amended).
/// Alias must be unique within a budget including soft-deleted accounts.
/// </summary>
public sealed class AliasUniquenessTests : CurrentSituationTestBase
{
    public AliasUniquenessTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBankAccount_DuplicateAliasOfActiveAccount_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-create1@example.com");
        await CreateBankAccountAsync(budgetId, "Savings", displayOrder: 1);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = "Savings", isPositive = true, displayOrder = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateBankAccount_DuplicateAliasOfSoftDeletedAccount_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-create2@example.com");
        var deletedId     = await CreateBankAccountAsync(budgetId, "OldChecking", displayOrder: 1);
        await DeleteBankAccountAsync(budgetId, deletedId);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = "OldChecking", isPositive = true, displayOrder = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateBankAccount_UniqueAlias_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-create3@example.com");
        await CreateBankAccountAsync(budgetId, "Savings", displayOrder: 1);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new { currencyId = GtqId, alias = "Checking", isPositive = true, displayOrder = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBankAccount_AliasOfAnotherActiveAccount_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-update1@example.com");
        var accountAId    = await CreateBankAccountAsync(budgetId, "Checking", displayOrder: 1);
        await CreateBankAccountAsync(budgetId, "Savings", displayOrder: 2);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountAId}",
            new { alias = "Savings", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateBankAccount_AliasOfSoftDeletedAccount_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-update2@example.com");
        var accountAId    = await CreateBankAccountAsync(budgetId, "Checking", displayOrder: 1);
        var deletedId     = await CreateBankAccountAsync(budgetId, "Archived", displayOrder: 2);
        await DeleteBankAccountAsync(budgetId, deletedId);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountAId}",
            new { alias = "Archived", isPositive = true, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateBankAccount_OwnAlias_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("alias-update3@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "Checking", displayOrder: 1);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}",
            new { alias = "Checking", isPositive = false, displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
