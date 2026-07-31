using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Features.BudgetStructure;
using MyBudget.Integration.Tests.Infrastructure;

namespace MyBudget.Integration.Tests.Features.CurrentSituation;

/// <summary>
/// Test base for CurrentSituation integration tests.
/// Extends BudgetStructureTestBase to reuse budget/cycle/period setup.
/// Adds helpers for BankAccounts and CutRecords.
/// </summary>
public abstract class CurrentSituationTestBase : BudgetStructureTestBase
{
    protected static readonly Guid GtqId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid UsdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected CurrentSituationTestBase(IntegrationTestFactory factory) : base(factory) { }

    // ── Auth helpers ──────────────────────────────────────────────────────────

    /// <summary>Registers a user with Operator role on the given budget and returns their token.</summary>
    protected async Task<string> SetupOperatorAsync(Guid budgetId, string email)
    {
        var login = await RegisterUserAsync(email);

        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership  = BudgetMembership.Create(budgetId, login.User.Id, BudgetRole.Operator);
        db.BudgetMemberships.Add(membership);
        await db.SaveChangesAsync();

        return login.AccessToken;
    }

    // ── Cycle helpers with alternate currency ─────────────────────────────────

    protected async Task<Guid> CreateCycleWithAlternateCurrencyAsync(
        Guid     budgetId,
        string   name         = "Cycle 2026",
        DateOnly? start       = null,
        DateOnly? end         = null)
    {
        var s = start ?? new DateOnly(2026, 1, 1);
        var e = end   ?? new DateOnly(2026, 12, 31);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new
            {
                name,
                startDate           = s,
                endDate             = e,
                defaultCurrencyId   = GtqId,
                alternateCurrencyId = UsdId,
                exchangeRate        = 7.8m,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── Setup: active cycle + period covering the test cut date ───────────────

    protected async Task<(Guid CycleId, Guid PeriodId)> SetupActiveCycleAndPeriodAsync(
        Guid     budgetId,
        DateOnly cutDate)
    {
        var periodStart = cutDate.AddDays(-15);
        var periodEnd   = cutDate.AddDays(15);
        var cycleStart  = new DateOnly(cutDate.Year, 1, 1);
        var cycleEnd    = new DateOnly(cutDate.Year, 12, 31);

        var cycleId = await CreateCycleWithAlternateCurrencyAsync(
            budgetId, "Test Cycle", cycleStart, cycleEnd);

        // Activate cycle
        await Client.PutAsJsonAsync($"/api/budgets/{budgetId}/active-cycle", new { cycleId });

        var periodId = await CreatePeriodAsync(
            budgetId, cycleId, "Test Period", 1, periodStart, periodEnd);

        return (cycleId, periodId);
    }

    // ── BankAccount helpers ───────────────────────────────────────────────────

    protected async Task<Guid> CreateBankAccountAsync(
        Guid   budgetId,
        string alias        = "Caja GTQ",
        Guid?  currencyId   = null,
        bool   isPositive   = true,
        int    displayOrder = 1)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/bank-accounts",
            new
            {
                currencyId   = currencyId ?? GtqId,
                alias,
                isPositive,
                displayOrder,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    protected async Task DeleteBankAccountAsync(Guid budgetId, Guid accountId)
    {
        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/bank-accounts/{accountId}");
        response.EnsureSuccessStatusCode();
    }

    // ── CutRecord helpers ─────────────────────────────────────────────────────

    protected async Task<System.Net.Http.HttpResponseMessage> UpsertCutRecordAsync(
        Guid     budgetId,
        DateOnly cutDate,
        decimal  exchangeRate = 7.8m,
        IEnumerable<(Guid AccountId, decimal Balance)>? accounts = null)
    {
        var accountList = (accounts ?? Enumerable.Empty<(Guid, decimal)>())
            .Select(a => new { bankAccountId = a.AccountId, balance = a.Balance })
            .ToList();

        return await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/cut-records/{cutDate:yyyy-MM-dd}",
            new
            {
                exchangeRate,
                projectionsJson = (string?)null,
                accounts        = accountList,
            });
    }

    protected async Task<System.Net.Http.HttpResponseMessage> GetCutRecordAsync(
        Guid budgetId, DateOnly cutDate)
        => await Client.GetAsync(
            $"/api/budgets/{budgetId}/cut-records/{cutDate:yyyy-MM-dd}");

    // ── CS-9 / CS-6 test helpers: direct DB access for the persisted 16 columns ──
    // The GET response DTO only exposes 6 + 3 of the 16 persisted totals (design.md
    // "Response DTOs are unchanged"); reaching CutRecord directly is required to assert
    // on the remaining columns (Alt execution trio, TotalAvailable/TotalNet + Alt).

    /// <summary>Reads the persisted CutRecord entity (all 16 total columns) directly via EF.</summary>
    protected async Task<CutRecord?> GetPersistedCutRecordEntityAsync(Guid budgetId, DateOnly cutDate)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CutRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(cr => cr.BudgetId == budgetId && cr.CutDate == cutDate);
    }

    /// <summary>
    /// Directly overwrites a persisted CutBankAccount snapshot's balance — the closest
    /// analog to "editing a bank account balance" after a cut is saved. BankAccount itself
    /// has no live stored Balance; balances only ever exist as immutable per-cut snapshots.
    /// </summary>
    protected async Task MutateCutBankAccountBalanceAsync(Guid bankAccountId, decimal newBalanceInPrimary)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "CutBankAccounts" SET "BalanceInPrimary" = {newBalanceInPrimary} WHERE "BankAccountId" = {bankAccountId}""");
    }

    /// <summary>
    /// Directly overwrites all 16 persisted total columns on a CutRecord header row to a
    /// single marker value — used to prove a read path does or does not re-derive totals.
    /// </summary>
    protected async Task MutateCutRecordHeaderTotalsAsync(Guid cutRecordId, decimal marker)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CutRecords" SET
                "TotalPositive" = {marker}, "TotalPositiveAlt" = {marker},
                "TotalNegative" = {marker}, "TotalNegativeAlt" = {marker},
                "TotalDeudaEnCurso" = {marker}, "TotalDeudaEnCursoAlt" = {marker},
                "TotalBudgeted" = {marker}, "TotalBudgetedAlt" = {marker},
                "TotalRegistered" = {marker}, "TotalRegisteredAlt" = {marker},
                "Remaining" = {marker}, "RemainingAlt" = {marker},
                "TotalAvailable" = {marker}, "TotalAvailableAlt" = {marker},
                "TotalNet" = {marker}, "TotalNetAlt" = {marker}
            WHERE "Id" = {cutRecordId}
            """);
    }

    /// <summary>
    /// Counts, at the Postgres schema level, how many of the 16 persisted total columns on
    /// "CutRecords" are declared NOT NULL — used by the CS-9 post-migration assertion.
    /// </summary>
    protected async Task<int> CountNonNullCutRecordTotalColumnsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_name = 'CutRecords' AND is_nullable = 'NO'
              AND column_name = ANY (ARRAY[
                'TotalPositive','TotalPositiveAlt','TotalNegative','TotalNegativeAlt',
                'TotalDeudaEnCurso','TotalDeudaEnCursoAlt','TotalBudgeted','TotalBudgetedAlt',
                'TotalRegistered','TotalRegisteredAlt','Remaining','RemainingAlt',
                'TotalAvailable','TotalAvailableAlt','TotalNet','TotalNetAlt'
              ])
            """;
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // ── ExecutionRecord helpers (needed for CS-6 snapshot / CS-9 backfill setup) ──
    // CurrentSituationTestBase does not extend BudgetExecutionTestBase (C# has no multiple
    // inheritance), so the minimal Create/Update helpers are duplicated here.

    protected async Task<Guid> CreateExecutionRecordAsync(
        Guid     budgetId,
        Guid     periodId,
        Guid     lineId,
        decimal  amount,
        DateOnly operationDate,
        int      entryType = 1) // 1 = Expense
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType,
                amount,
                note            = "Test execution note",
                operationDate,
                currencyId      = GtqId,
                exchangeRate    = (decimal?)null,
                exchangeRateTo  = (decimal?)null,
                accountId       = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    protected async Task UpdateExecutionRecordAsync(
        Guid     budgetId,
        Guid     periodId,
        Guid     lineId,
        Guid     executionId,
        decimal  amount,
        DateOnly operationDate,
        int      entryType = 1)
    {
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}",
            new
            {
                entryType,
                amount,
                note            = "Updated execution note",
                operationDate,
                currencyId      = GtqId,
                exchangeRate    = (decimal?)null,
                exchangeRateTo  = (decimal?)null,
                accountId       = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Composes SetupActiveCycleAndPeriodAsync with a category group + budget line so
    /// tests can predict a non-zero TotalBudgeted/Remaining for the CS-6 total concepts.
    /// </summary>
    protected async Task<(Guid CycleId, Guid PeriodId, Guid GroupId, Guid LineId)> SetupPeriodWithBudgetLineAsync(
        Guid budgetId, DateOnly cutDate, decimal budgetedAmount)
    {
        var (cycleId, periodId) = await SetupActiveCycleAndPeriodAsync(budgetId, cutDate);
        var groupId = await CreateCategoryGroupAsync(budgetId);
        var lineId  = await CreateBudgetLineAsync(budgetId, periodId, groupId, amount: budgetedAmount);
        return (cycleId, periodId, groupId, lineId);
    }

    // ── Response types ────────────────────────────────────────────────────────

    protected sealed record ErrorResponse(string Error);

    protected sealed record BankAccountListItem(
        Guid              Id,
        Guid              CurrencyId,
        string            Alias,
        bool              IsPositive,
        int               DisplayOrder,
        DateTimeOffset?   DeletedAt);

    protected sealed record CutRecordResponse(
        bool                            IsDraft,
        Guid?                           CutRecordId,
        DateOnly                        CutDate,
        decimal                         ExchangeRate,
        string?                         ProjectionsJson,
        ExecutionSummaryDto             ExecutionSummary,
        IReadOnlyList<CutAccountItem>   Accounts,
        CutTotalsDto                    Totals);

    protected sealed record ExecutionSummaryDto(
        decimal TotalBudgeted,
        decimal TotalRegistered,
        decimal Remaining);

    protected sealed record CutAccountItem(
        Guid    BankAccountId,
        string  Alias,
        Guid    CurrencyId,
        bool    IsPositive,
        int     DisplayOrder,
        decimal Balance,
        decimal BalanceInPrimary);

    protected sealed record CutTotalsDto(
        decimal TotalPositive,
        decimal TotalNegative,
        decimal TotalDeudaEnCurso,
        decimal TotalPositiveAlt,
        decimal TotalNegativeAlt,
        decimal TotalDeudaEnCursoAlt);
}
