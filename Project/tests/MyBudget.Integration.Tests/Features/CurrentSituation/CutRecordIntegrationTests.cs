using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.CurrentSituation;

/// <summary>
/// Integration tests for CutRecord endpoints.
/// Covers spec CS-1 through CS-4.
/// </summary>
public sealed class CutRecordIntegrationTests : CurrentSituationTestBase
{
    public CutRecordIntegrationTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    private static readonly DateOnly TestCutDate = new DateOnly(2026, 7, 28);

    // ── CS-1: Upsert Cut Record ───────────────────────────────────────────────

    [Fact]
    public async Task UpsertCutRecord_ValidPayloadWithActivePeriod_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-upsert1@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        var response = await UpsertCutRecordAsync(
            budgetId, TestCutDate, 7.8m, [(accountId, 5000m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

    }

    [Fact]
    public async Task UpsertCutRecord_NoActivePeriod_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-upsert2@example.com");
        // No cycle created — no active period

        var response = await UpsertCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("NO_ACTIVE_PERIOD_FOR_CUT_DATE");
    }

    [Fact]
    public async Task UpsertCutRecord_ReadRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-upsert3-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "cs-upsert3-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await UpsertCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpsertCutRecord_Replace_OverwritesAllCutBankAccountRows()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-upsert4@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var account1 = await CreateBankAccountAsync(budgetId, "Account 1", displayOrder: 1);
        var account2 = await CreateBankAccountAsync(budgetId, "Account 2", displayOrder: 2);

        // First upsert: both accounts
        var first = await UpsertCutRecordAsync(
            budgetId, TestCutDate, 7.8m,
            [(account1, 1000m), (account2, 2000m)]);
        first.EnsureSuccessStatusCode();

        // Second upsert: only account1 with a different balance
        var second = await UpsertCutRecordAsync(
            budgetId, TestCutDate, 8.0m,
            [(account1, 1500m)]);
        second.EnsureSuccessStatusCode();

        // Verify: only account1 with updated balance
        var getResp = await GetCutRecordAsync(budgetId, TestCutDate);
        var cut     = await getResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);

        cut!.IsDraft.ShouldBeFalse();
        cut.Accounts.Count.ShouldBe(1);
        cut.Accounts[0].BankAccountId.ShouldBe(account1);
        cut.Accounts[0].Balance.ShouldBe(1500m);
        cut.ExchangeRate.ShouldBe(8.0m);
    }

    // ── CS-2: Get Cut Record (existing) ──────────────────────────────────────

    [Fact]
    public async Task GetCutRecord_Existing_ReturnsPersistedBalancesAndIsDraftFalse()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-get1@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 3500m)]))
            .EnsureSuccessStatusCode();

        var response = await GetCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeFalse();
        cut.CutRecordId.ShouldNotBeNull();
        cut.Accounts.Count.ShouldBe(1);
        cut.Accounts[0].Balance.ShouldBe(3500m);
        cut.Accounts[0].BalanceInPrimary.ShouldBe(3500m); // GTQ = primary
    }

    // ── CS-2: Get Cut Record (draft — first ever) ─────────────────────────────

    [Fact]
    public async Task GetCutRecord_Draft_FirstEver_AllActiveAccountsWithZeroBalance()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-get2@example.com");
        var accountId1    = await CreateBankAccountAsync(budgetId, "Account 1", displayOrder: 1);
        var accountId2    = await CreateBankAccountAsync(budgetId, "Account 2", displayOrder: 2);

        var response = await GetCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeTrue();
        cut.CutRecordId.ShouldBeNull();
        cut.Accounts.Count.ShouldBe(2);
        cut.Accounts.ShouldAllBe(a => a.Balance == 0m);
        cut.Accounts.Any(a => a.BankAccountId == accountId1).ShouldBeTrue();
        cut.Accounts.Any(a => a.BankAccountId == accountId2).ShouldBeTrue();
    }

    // ── CS-2: Get Cut Record (draft — cloned from previous cut) ──────────────

    [Fact]
    public async Task GetCutRecord_Draft_ClonedFromPreviousCut_WithNewAccountAtZero()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-get3@example.com");
        var prevDate      = new DateOnly(2026, 7, 25);
        await SetupActiveCycleAndPeriodAsync(budgetId, prevDate);

        var accountA = await CreateBankAccountAsync(budgetId, "Account A", displayOrder: 1);

        // Create cut for prevDate with accountA balance=2000
        (await UpsertCutRecordAsync(budgetId, prevDate, 7.8m, [(accountA, 2000m)]))
            .EnsureSuccessStatusCode();

        // Add accountB AFTER the previous cut
        var accountB = await CreateBankAccountAsync(budgetId, "Account B", displayOrder: 2);

        // Get draft for a later date
        var response = await GetCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeTrue();
        cut.Accounts.Count.ShouldBe(2);

        var clonedA = cut.Accounts.Single(a => a.BankAccountId == accountA);
        clonedA.Balance.ShouldBe(2000m); // cloned from prev cut

        var newB = cut.Accounts.Single(a => a.BankAccountId == accountB);
        newB.Balance.ShouldBe(0m); // new account gets zero
    }

    [Fact]
    public async Task GetCutRecord_Draft_SoftDeletedAccountExcluded()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-get4@example.com");
        var prevDate      = new DateOnly(2026, 7, 25);
        await SetupActiveCycleAndPeriodAsync(budgetId, prevDate);

        var accountA = await CreateBankAccountAsync(budgetId, "Account A", displayOrder: 1);
        var accountC = await CreateBankAccountAsync(budgetId, "Account C (to delete)", displayOrder: 2);

        // Create previous cut with both accounts
        (await UpsertCutRecordAsync(budgetId, prevDate, 7.8m,
            [(accountA, 1000m), (accountC, 500m)])).EnsureSuccessStatusCode();

        // Soft-delete accountC
        await DeleteBankAccountAsync(budgetId, accountC);

        // Get draft for later date
        var response = await GetCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeTrue();
        cut.Accounts.ShouldNotContain(a => a.BankAccountId == accountC);
        cut.Accounts.ShouldContain(a => a.BankAccountId == accountA);
    }

    // ── CS-2: Get Cut Record (no active period — execution summary zeroed) ────

    [Fact]
    public async Task GetCutRecord_NoActivePeriod_ExecutionSummaryIsZero()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-get5@example.com");
        // No cycle/period — cut date is uncovered

        var response = await GetCutRecordAsync(budgetId, TestCutDate);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.ExecutionSummary.TotalBudgeted.ShouldBe(0m);
        cut.ExecutionSummary.TotalRegistered.ShouldBe(0m);
        cut.ExecutionSummary.Remaining.ShouldBe(0m);
    }

    // ── CS-3: List Cut Dates ──────────────────────────────────────────────────

    [Fact]
    public async Task ListCutDates_ReturnsDatesAscending()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-dates1@example.com");
        var date1 = new DateOnly(2026, 7, 15);
        var date2 = new DateOnly(2026, 7, 20);
        var date3 = new DateOnly(2026, 7, 28);
        await SetupActiveCycleAndPeriodAsync(budgetId, date1);

        (await UpsertCutRecordAsync(budgetId, date3)).EnsureSuccessStatusCode();
        (await UpsertCutRecordAsync(budgetId, date1)).EnsureSuccessStatusCode();
        (await UpsertCutRecordAsync(budgetId, date2)).EnsureSuccessStatusCode();

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cut-records/dates");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dates = await response.Content.ReadFromJsonAsync<DateOnly[]>(JsonOpts);
        dates!.Length.ShouldBe(3);
        dates[0].ShouldBe(date1);
        dates[1].ShouldBe(date2);
        dates[2].ShouldBe(date3);
    }

    [Fact]
    public async Task ListCutDates_NoCuts_ReturnsEmptyList()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-dates2@example.com");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/cut-records/dates");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dates = await response.Content.ReadFromJsonAsync<DateOnly[]>(JsonOpts);
        dates!.ShouldBeEmpty();
    }

    // ── CS-4: Delete Cut Record ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteCutRecord_RemovesRecordAndCutBankAccountRows()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-delete1@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId);

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 1000m)]))
            .EnsureSuccessStatusCode();

        var deleteResp = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/cut-records/{TestCutDate:yyyy-MM-dd}");

        deleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify: subsequent GET returns a draft (no record exists)
        var getResp = await GetCutRecordAsync(budgetId, TestCutDate);
        var cut     = await getResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.IsDraft.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteCutRecord_NonExistentDate_Returns404()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-delete2@example.com");

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/cut-records/{TestCutDate:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCutRecord_ReadRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs-delete3-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "cs-delete3-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/cut-records/{TestCutDate:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
