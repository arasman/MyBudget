using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.Features.CurrentSituation.Shared;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.CurrentSituation;

/// <summary>
/// Integration tests for CutRecord endpoints.
/// Covers spec CS-1 through CS-4, plus CS-6 (persisted totals) and CS-9 (migration backfill).
/// </summary>
public sealed class CutRecordIntegrationTests : CurrentSituationTestBase
{
    public CutRecordIntegrationTests(Infrastructure.IntegrationTestFactory factory)
        : base(factory) { }

    private static readonly DateOnly TestCutDate = new DateOnly(2026, 7, 28);

    /// <summary>Projects a persisted CutRecord entity's 16 total columns into a CutTotals record for value comparison.</summary>
    private static CutTotals ToCutTotals(CutRecord cr) => new(
        cr.TotalPositive,        cr.TotalPositiveAlt,
        cr.TotalNegative,        cr.TotalNegativeAlt,
        cr.TotalDeudaEnCurso,    cr.TotalDeudaEnCursoAlt,
        cr.TotalBudgeted,        cr.TotalBudgetedAlt,
        cr.TotalRegistered,      cr.TotalRegisteredAlt,
        cr.Remaining,            cr.RemainingAlt,
        cr.TotalAvailable,       cr.TotalAvailableAlt,
        cr.TotalNet,             cr.TotalNetAlt);

    /// <summary>
    /// The Phase B backfill logic from Migrations/20260730233923_AddCutRecordPersistedTotals.cs,
    /// duplicated here (design.md's accepted duplication — dead code once the real migration
    /// has run once). Correlation is via "src" (a plain "CutRecords" self-join FROM entry), not
    /// the UPDATE target "cr" directly — Postgres rejects LATERAL correlating straight to the
    /// UPDATE target (42P10); this shape is required, not merely a style choice. One further
    /// deliberate adaptation: the real migration's fallback zero-fill UPDATE matches
    /// "TotalPositive" IS NULL (columns were nullable at that point in its own 3-phase run);
    /// here the columns are already NOT NULL (this migration already applied to the test DB),
    /// so the fallback matches the -1 sentinel used to seed "not yet backfilled" rows instead.
    /// Re-executed against a sentinel-seeded row to simulate "run the migration against a
    /// pre-seeded broken state" for CS-9.
    /// </summary>
    private const string BackfillSql = """
        UPDATE "CutRecords" cr
        SET
            "TotalPositive"        = ROUND(x."Pos", 2),
            "TotalPositiveAlt"     = ROUND(x."Pos" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalNegative"        = ROUND(x."Neg", 2),
            "TotalNegativeAlt"     = ROUND(x."Neg" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalDeudaEnCurso"    = ROUND(x."Remaining" + x."Neg", 2),
            "TotalDeudaEnCursoAlt" = ROUND((x."Remaining" + x."Neg") / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalBudgeted"        = ROUND(x."TotalBudgeted", 2),
            "TotalBudgetedAlt"     = ROUND(x."TotalBudgeted" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalRegistered"      = ROUND(x."TotalRegistered", 2),
            "TotalRegisteredAlt"   = ROUND(x."TotalRegistered" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "Remaining"            = ROUND(x."Remaining", 2),
            "RemainingAlt"         = ROUND(x."Remaining" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalAvailable"       = ROUND(x."Pos", 2),
            "TotalAvailableAlt"    = ROUND(x."Pos" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
            "TotalNet"             = ROUND(x."Pos" - (x."Remaining" + x."Neg"), 2),
            "TotalNetAlt"          = ROUND((x."Pos" - (x."Remaining" + x."Neg")) / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2)
        FROM (
            SELECT
                src."Id" AS "CutRecordId",
                a."Pos", a."Neg",
                COALESCE(ex."TotalBudgeted", 0)   AS "TotalBudgeted",
                COALESCE(ex."TotalRegistered", 0) AS "TotalRegistered",
                COALESCE(ex."Remaining", 0)       AS "Remaining"
            FROM "CutRecords" src
            JOIN (
                SELECT
                    cba."CutRecordId",
                    COALESCE(SUM(cba."BalanceInPrimary") FILTER (WHERE cba."IsPositive"), 0)     AS "Pos",
                    COALESCE(SUM(cba."BalanceInPrimary") FILTER (WHERE NOT cba."IsPositive"), 0) AS "Neg"
                FROM "CutBankAccounts" cba
                GROUP BY cba."CutRecordId"
            ) a ON a."CutRecordId" = src."Id"
            LEFT JOIN LATERAL (
                WITH active_period AS (
                    SELECT
                        p."Id"                 AS "PeriodId",
                        p."StartDate"          AS "PeriodStart",
                        p."EndDate"            AS "PeriodEnd",
                        cy."DefaultCurrencyId" AS "DefaultCurrencyId"
                    FROM "Periods" p
                    JOIN "Cycles" cy ON cy."Id" = p."CycleId"
                    WHERE cy."BudgetId"  = src."BudgetId"
                      AND cy."DeletedAt" IS NULL
                      AND p."DeletedAt"  IS NULL
                      AND p."IsClosed"   = false
                      AND p."StartDate"  <= src."CutDate"
                      AND p."EndDate"    >= src."CutDate"
                    LIMIT 1
                ),
                budgeted AS (
                    SELECT COALESCE(SUM(rev."BudgetedAmount"), 0) AS "TotalBudgeted"
                    FROM "BudgetLines" bl
                    JOIN active_period ap ON true
                    LEFT JOIN LATERAL (
                        SELECT r."BudgetedAmount"
                        FROM "BudgetLineRevisions" r
                        WHERE r."BudgetLineId" = bl."Id"
                          AND r."ValidFrom"::date <= ap."PeriodStart"
                          AND (r."ValidTo" IS NULL OR r."ValidTo"::date >= ap."PeriodStart")
                        LIMIT 1
                    ) rev ON true
                    WHERE bl."BudgetId"  = src."BudgetId"
                      AND bl."DeletedAt" IS NULL
                      AND bl."StartDate"::date <= ap."PeriodEnd"
                      AND (bl."EndDate" IS NULL OR bl."EndDate"::date >= ap."PeriodStart")
                ),
                registered AS (
                    SELECT COALESCE(SUM(
                        CASE
                            WHEN e."EntryType" = 1 THEN
                                CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                    THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END
                            WHEN e."EntryType" = 3 THEN
                                CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                    THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END
                            WHEN e."EntryType" = 2 THEN
                                -(CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                    THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END)
                            ELSE 0
                        END
                    ), 0) AS "TotalRegistered"
                    FROM "ExecutionRecords" e
                    JOIN active_period ap ON ap."PeriodId" = e."PeriodId"
                    WHERE e."BudgetId"  = src."BudgetId"
                      AND e."DeletedAt" IS NULL
                )
                SELECT
                    b."TotalBudgeted",
                    r."TotalRegistered",
                    (b."TotalBudgeted" - r."TotalRegistered") AS "Remaining"
                FROM budgeted b
                CROSS JOIN registered r
            ) ex ON true
        ) x
        WHERE cr."Id" = x."CutRecordId";

        UPDATE "CutRecords" SET
            "TotalPositive" = 0, "TotalPositiveAlt" = 0,
            "TotalNegative" = 0, "TotalNegativeAlt" = 0,
            "TotalDeudaEnCurso" = 0, "TotalDeudaEnCursoAlt" = 0,
            "TotalBudgeted" = 0, "TotalBudgetedAlt" = 0,
            "TotalRegistered" = 0, "TotalRegisteredAlt" = 0,
            "Remaining" = 0, "RemainingAlt" = 0,
            "TotalAvailable" = 0, "TotalAvailableAlt" = 0,
            "TotalNet" = 0, "TotalNetAlt" = 0
        WHERE "TotalPositive" = -1;
        """;

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

    // ── CS-1: Re-save + client-submitted totals ignored ──────────────────────

    [Fact]
    public async Task UpsertCutRecord_ClientSubmittedTotals_AreIgnored()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs1-ignore1@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        // Body includes total fields alongside balances — the request DTO has no matching
        // properties, so the JSON binder drops them; the server always computes fresh values.
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/cut-records/{TestCutDate:yyyy-MM-dd}",
            new
            {
                exchangeRate = 7.8m,
                projectionsJson = (string?)null,
                accounts = new[] { new { bankAccountId = accountId, balance = 500m } },
                totalPositive        = 999999m,
                totalPositiveAlt     = 999999m,
                totalNegative        = 999999m,
                totalDeudaEnCurso    = 999999m,
                totalBudgeted        = 999999m,
                totalRegistered      = 999999m,
                remaining            = 999999m,
                totalAvailable       = 999999m,
                totalNet             = 999999m,
            });

        response.EnsureSuccessStatusCode();

        var persisted = await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate);
        persisted.ShouldNotBeNull();
        persisted!.TotalPositive.ShouldBe(500m);  // server-computed from the account balance
        persisted.TotalNegative.ShouldBe(0m);     // not the submitted 999999
        persisted.TotalBudgeted.ShouldBe(0m);     // no budget line set up -> live-computed 0
        persisted.TotalAvailable.ShouldBe(500m);
    }

    [Fact]
    public async Task UpsertCutRecord_ReSave_OverwritesAllSixteenPersistedTotals()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs1-resave1@example.com");
        var (_, periodId, _, lineId) = await SetupPeriodWithBudgetLineAsync(budgetId, TestCutDate, budgetedAmount: 500m);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, amount: 200m, operationDate: TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 500m)]))
            .EnsureSuccessStatusCode();
        var firstTotals = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);

        // Re-save with a different balance -> every one of the 16 totals must be overwritten.
        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 1500m)]))
            .EnsureSuccessStatusCode();
        var secondTotals = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);

        secondTotals.ShouldNotBe(firstTotals);
        secondTotals.TotalPositive.ShouldBe(1500m);
        secondTotals.TotalAvailable.ShouldBe(1500m);

        var expectedSummary = new BudgetExecutionSummary(TotalBudgeted: 500m, TotalRegistered: 200m, Remaining: 300m);
        var expected = CutTotalsCalculator.Compute(
            new (bool IsPositive, decimal BalanceInPrimary)[] { (true, 1500m) }, expectedSummary, 7.8m);

        secondTotals.ShouldBe(expected);
    }

    // ── CS-6: Cut Totals — persisted == freshly computed, snapshot semantics ──

    [Fact]
    public async Task UpsertCutRecord_PersistedTotals_EqualFreshlyComputedTotals()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs6-totals1@example.com");
        var (_, periodId, _, lineId) = await SetupPeriodWithBudgetLineAsync(budgetId, TestCutDate, budgetedAmount: 500m);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, amount: 200m, operationDate: TestCutDate);

        var accountPos = await CreateBankAccountAsync(budgetId, "Positive", isPositive: true, displayOrder: 1);
        var accountNeg = await CreateBankAccountAsync(budgetId, "Negative", isPositive: false, displayOrder: 2);

        // CS-6 table case: A(IsPositive=true, 500), B(IsPositive=false, 200), Remaining=300
        // (Budgeted 500 - Registered 200) -> TotalPositive=500, TotalNegative=200, TotalDeudaEnCurso=500
        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m,
            [(accountPos, 500m), (accountNeg, 200m)])).EnsureSuccessStatusCode();

        var persisted = await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate);
        persisted.ShouldNotBeNull();

        var expectedSummary = new BudgetExecutionSummary(TotalBudgeted: 500m, TotalRegistered: 200m, Remaining: 300m);
        var expected = CutTotalsCalculator.Compute(
            new (bool IsPositive, decimal BalanceInPrimary)[] { (true, 500m), (false, 200m) },
            expectedSummary, 7.8m);

        ToCutTotals(persisted!).ShouldBe(expected);
    }

    [Fact]
    public async Task PersistedCutTotals_UnaffectedByLaterAccountOrExecutionEdits()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs6-snapshot1@example.com");
        var (_, periodId, _, lineId) = await SetupPeriodWithBudgetLineAsync(budgetId, TestCutDate, budgetedAmount: 500m);
        var executionId = await CreateExecutionRecordAsync(budgetId, periodId, lineId, amount: 200m, operationDate: TestCutDate);
        var accountId   = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 500m)]))
            .EnsureSuccessStatusCode();

        var savedTotals = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);

        // Edit the execution record affecting the same active period AFTER the cut was saved.
        await UpdateExecutionRecordAsync(budgetId, periodId, lineId, executionId, amount: 999m, operationDate: TestCutDate);

        // Directly mutate the persisted CutBankAccount snapshot's balance — the closest analog
        // to "editing a bank account balance" (BankAccount itself has no live stored Balance;
        // balances only exist as immutable per-cut CutBankAccount snapshots).
        await MutateCutBankAccountBalanceAsync(accountId, 9999m);

        var totalsAfterEdits = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);
        totalsAfterEdits.ShouldBe(savedTotals);

        // The GET response for the existing cut also still reflects the frozen totals.
        var getResp = await GetCutRecordAsync(budgetId, TestCutDate);
        var cut = await getResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        cut!.Totals.TotalPositive.ShouldBe(savedTotals.TotalPositive);
        cut.ExecutionSummary.Remaining.ShouldBe(savedTotals.Remaining);
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

    [Fact]
    public async Task GetCutRecord_Existing_ReturnsStoredColumnsVerbatim_NotRecomputed()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs2-verbatim1@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 500m)]))
            .EnsureSuccessStatusCode();

        var saved = await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate);
        saved.ShouldNotBeNull();

        // Overwrite the persisted header totals to a marker value that a live recompute from
        // CutBankAccounts/execution data would never produce (500 was saved; the account's
        // BalanceInPrimary is still 500). If GetCutRecord re-ran the aggregation/CTE for an
        // existing record it would return 500, not the marker.
        await MutateCutRecordHeaderTotalsAsync(saved!.Id, marker: 424242.42m);

        var response = await GetCutRecordAsync(budgetId, TestCutDate);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cut = await response.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);

        cut!.IsDraft.ShouldBeFalse();
        cut.Totals.TotalPositive.ShouldBe(424242.42m);
        cut.Totals.TotalNegative.ShouldBe(424242.42m);
        cut.Totals.TotalDeudaEnCurso.ShouldBe(424242.42m);
        cut.ExecutionSummary.TotalBudgeted.ShouldBe(424242.42m);
        cut.ExecutionSummary.TotalRegistered.ShouldBe(424242.42m);
        cut.ExecutionSummary.Remaining.ShouldBe(424242.42m);
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

    [Fact]
    public async Task GetCutRecord_Draft_ComputesAllEightTotalConceptsLive()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs2-draft-live1@example.com");
        var (_, periodId, _, lineId) = await SetupPeriodWithBudgetLineAsync(budgetId, TestCutDate, budgetedAmount: 500m);
        var executionId = await CreateExecutionRecordAsync(budgetId, periodId, lineId, amount: 100m, operationDate: TestCutDate);

        // No cut has ever been saved for TestCutDate -> draft. No persisted snapshot exists to
        // freeze anything, so the execution summary and totals must be computed live.
        var firstResp = await GetCutRecordAsync(budgetId, TestCutDate);
        var firstCut  = await firstResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        firstCut!.IsDraft.ShouldBeTrue();
        firstCut.ExecutionSummary.TotalBudgeted.ShouldBe(500m);
        firstCut.ExecutionSummary.TotalRegistered.ShouldBe(100m);
        firstCut.ExecutionSummary.Remaining.ShouldBe(400m); // 500 - 100

        // Change the execution data — since there is no persisted cut, the draft must reflect
        // this immediately on the next GET (unlike the frozen existing-record path in CS-6).
        await UpdateExecutionRecordAsync(budgetId, periodId, lineId, executionId, amount: 300m, operationDate: TestCutDate);

        var secondResp = await GetCutRecordAsync(budgetId, TestCutDate);
        var secondCut  = await secondResp.Content.ReadFromJsonAsync<CutRecordResponse>(JsonOpts);
        secondCut!.IsDraft.ShouldBeTrue();
        secondCut.ExecutionSummary.TotalBudgeted.ShouldBe(500m);
        secondCut.ExecutionSummary.TotalRegistered.ShouldBe(300m);
        secondCut.ExecutionSummary.Remaining.ShouldBe(200m); // 500 - 300, live recompute
        secondCut.Totals.TotalDeudaEnCurso.ShouldBe(200m);   // Remaining(200) + TotalNegative(0)
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

    // ── CS-9: Migration Backfill ──────────────────────────────────────────────
    //
    // The migration's 3-phase Up() (add nullable columns -> SQL backfill -> AlterColumn
    // non-nullable) cannot be re-run mid-suite: by the time any test runs, IntegrationTestFactory
    // has already applied every migration and the 16 columns are already NOT NULL in the test
    // DB's schema. The pragmatic equivalent used here: seed a row exactly as the pre-migration
    // world would have looked (a CutRecord + CutBankAccounts + execution data, with the total
    // columns holding a -1 sentinel standing in for "not yet backfilled"), then execute the
    // migration's Phase B backfill SQL (duplicated above as BackfillSql) directly against that
    // row, and assert the result matches what GetCutRecord would have computed for the same
    // underlying data pre-change.

    [Fact]
    public async Task MigrationBackfill_PreSeededRowsWithoutTotals_BackfilledToMatchPreChangeOutput()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs9-backfill1@example.com");
        var (_, periodId, _, lineId) = await SetupPeriodWithBudgetLineAsync(budgetId, TestCutDate, budgetedAmount: 500m);
        await CreateExecutionRecordAsync(budgetId, periodId, lineId, amount: 200m, operationDate: TestCutDate);

        var accountPos = await CreateBankAccountAsync(budgetId, "Positive", isPositive: true, displayOrder: 1);
        var accountNeg = await CreateBankAccountAsync(budgetId, "Negative", isPositive: false, displayOrder: 2);

        // Save the cut normally first — this stands in for "what pre-change GetCutRecord would
        // have computed live" from the same underlying CutBankAccounts + execution data.
        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m,
            [(accountPos, 500m), (accountNeg, 200m)])).EnsureSuccessStatusCode();

        var expected = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);
        var cutRecordId = (await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!.Id;

        // Simulate a pre-migration row: overwrite all 16 columns with a sentinel that no real
        // computation would ever produce, standing in for "no persisted totals yet".
        await MutateCutRecordHeaderTotalsAsync(cutRecordId, marker: -1m);
        var seeded = await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate);
        seeded!.TotalPositive.ShouldBe(-1m); // sentinel confirmed in place before backfill

        // Apply the migration's Phase B backfill SQL against the sentinel-seeded row.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(BackfillSql);
        }

        var backfilled = ToCutTotals((await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate))!);
        backfilled.ShouldBe(expected);
    }

    [Fact]
    public async Task MigrationBackfill_AllSixteenColumnsAreNonNullableInSchema()
    {
        var (_, budgetId) = await SetupOwnerAsync("cs9-backfill2@example.com");
        await SetupActiveCycleAndPeriodAsync(budgetId, TestCutDate);
        var accountId = await CreateBankAccountAsync(budgetId, "Caja GTQ");

        (await UpsertCutRecordAsync(budgetId, TestCutDate, 7.8m, [(accountId, 1000m)]))
            .EnsureSuccessStatusCode();

        var nonNullColumnCount = await CountNonNullCutRecordTotalColumnsAsync();
        nonNullColumnCount.ShouldBe(16);

        // Corroborate at the data level too: reading the row back through EF (whose CutRecord
        // properties are non-nullable decimals) succeeds without a materialization error, which
        // would be impossible if Postgres still allowed a NULL in any of the 16 columns.
        var persisted = await GetPersistedCutRecordEntityAsync(budgetId, TestCutDate);
        persisted.ShouldNotBeNull();
    }
}
