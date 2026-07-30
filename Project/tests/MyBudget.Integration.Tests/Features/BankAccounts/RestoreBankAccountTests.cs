using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Features.CurrentSituation;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BankAccounts;

/// <summary>
/// Integration tests for POST .../bank-accounts/{id}/restore.
/// Covers spec BA-5.
/// </summary>
public sealed class RestoreBankAccountTests : CurrentSituationTestBase
{
    public RestoreBankAccountTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    [Fact]
    public async Task RestoreSoftDeletedAccount_Returns204_And_ClearsDeletedAt()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-restore1@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "To Restore");

        await DeleteBankAccountAsync(budgetId, accountId);

        var response = await Client.PostAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}/restore", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify account is active in DB
        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account     = await db.BankAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == accountId);

        account.ShouldNotBeNull();
        account!.DeletedAt.ShouldBeNull();
        account.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RestoreNonExistentAccount_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-restore2@example.com");
        var fakeId        = Guid.NewGuid();

        var response = await Client.PostAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{fakeId}/restore", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreActiveAccount_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-restore3@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "Active Account");

        // Account is active (not deleted) — restore should return 404
        var response = await Client.PostAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}/restore", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreAccount_OperatorRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("ba-restore4-owner@example.com");
        var accountId     = await CreateBankAccountAsync(budgetId, "Restricted Account");
        await DeleteBankAccountAsync(budgetId, accountId);

        var operatorToken = await SetupOperatorAsync(budgetId, "ba-restore4-op@example.com");
        AuthorizeClient(operatorToken);

        var response = await Client.PostAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}/restore", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
