using System.Net.Http.Json;
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

    // ── Response types ────────────────────────────────────────────────────────

    protected sealed record ErrorResponse(string Error);

    protected sealed record BankAccountListItem(
        Guid   Id,
        Guid   CurrencyId,
        string Alias,
        bool   IsPositive,
        int    DisplayOrder);

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
