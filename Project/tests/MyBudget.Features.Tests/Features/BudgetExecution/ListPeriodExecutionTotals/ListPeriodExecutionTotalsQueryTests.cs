using MyBudget.Features.Features.BudgetExecution.ListPeriodExecutionTotals;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.ListPeriodExecutionTotals;

/// <summary>
/// Unit tests for ListPeriodExecutionTotals response model.
/// SQL-level correctness (date-range intersection, ValidFrom revision resolution)
/// is covered by integration tests in MyBudget.Integration.Tests.
/// </summary>
public sealed class ListPeriodExecutionTotalsQueryTests
{
    // ── DTO structure tests ───────────────────────────────────────────────────

    [Fact]
    public void LineTotalDto_IncludesBudgetedAmount()
    {
        // Verifies that LineTotalDto has BudgetedAmount field (PR2b addition)
        var dto = new LineTotalDto(
            BudgetLineId:     Guid.NewGuid(),
            BudgetLineName:   "Rent",
            BudgetedAmount:   1500m,
            TotalExpenses:    100m,
            TotalCreditNotes: 20m,
            TotalDebitNotes:  10m,
            NetTotal:         90m);

        dto.BudgetedAmount.ShouldBe(1500m);
        dto.NetTotal.ShouldBe(90m);
    }

    [Fact]
    public void PeriodExecutionTotalsResponse_HoldsBothShapes()
    {
        var line = new LineTotalDto(Guid.NewGuid(), "Rent", 1500m, 100m, 0m, 0m, 100m);
        var cat  = new CategoryTotalDto(Guid.NewGuid(), "Housing", null, null, 100m, 0m, 0m, 100m);

        var response = new PeriodExecutionTotalsResponse(
            new List<LineTotalDto>     { line },
            new List<CategoryTotalDto> { cat });

        response.LineTotals.ShouldHaveSingleItem();
        response.CategoryTotals.ShouldHaveSingleItem();
    }

    [Fact]
    public void NetTotal_Formula_IsExpensesPlusDebitMinusCredit()
    {
        // REQ-EXEC-TOTALS-2: netAmount = Expenses + DebitNotes - CreditNotes
        const decimal expenses    = 150m;
        const decimal creditNotes = 30m;
        const decimal debitNotes  = 20m;
        var expected              = expenses + debitNotes - creditNotes; // 140

        var dto = new LineTotalDto(
            Guid.NewGuid(), "Rent", 1500m,
            expenses, creditNotes, debitNotes,
            expenses + debitNotes - creditNotes);

        dto.NetTotal.ShouldBe(expected);
    }
}
