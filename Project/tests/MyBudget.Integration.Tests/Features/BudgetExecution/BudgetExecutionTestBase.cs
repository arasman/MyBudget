using System.Net.Http.Json;
using MyBudget.Integration.Tests.Features.BudgetStructure;
using MyBudget.Integration.Tests.Infrastructure;

namespace MyBudget.Integration.Tests.Features.BudgetExecution;

/// <summary>
/// Shared helpers for BudgetExecution integration tests.
/// Extends BudgetStructureTestBase to reuse cycle/period/line setup.
/// </summary>
public abstract class BudgetExecutionTestBase : BudgetStructureTestBase
{
    protected BudgetExecutionTestBase(IntegrationTestFactory factory) : base(factory) { }

    // GTQ = DefaultCurrencyId used in cycles (seed value)
    protected static readonly Guid GtqId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid UsdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Creates an ExecutionRecord of type Expense (same currency as cycle default = no exchange rate).
    /// </summary>
    protected async Task<Guid> CreateExecutionRecordAsync(
        Guid      budgetId,
        Guid      periodId,
        Guid      lineId,
        decimal   amount        = 100m,
        int       entryType     = 1,    // 1=Expense, 2=CreditNote, 3=DebitNote
        string?   note          = "Test execution note",
        Guid?     currencyId    = null,
        DateOnly? operationDate = null)
    {
        var opDate = operationDate ?? new DateOnly(2025, 1, 15);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions",
            new
            {
                entryType,
                amount,
                note,
                operationDate   = opDate,
                currencyId      = currencyId ?? GtqId,
                exchangeRate    = (decimal?)null,
                exchangeRateTo  = (decimal?)null,
                accountId       = (Guid?)null,
                paymentMethodId = (Guid?)null,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    /// <summary>Closes the given period via the status endpoint.</summary>
    protected async Task ClosePeriodAsync(Guid budgetId, Guid cycleId, Guid periodId)
    {
        var response = await Client.PatchAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status",
            new { isClosed = true });
        response.EnsureSuccessStatusCode();
    }
}
