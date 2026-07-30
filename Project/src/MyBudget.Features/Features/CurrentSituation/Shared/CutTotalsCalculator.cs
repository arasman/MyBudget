using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.CurrentSituation.Shared;

/// <summary>
/// Pure static arithmetic for CS-6's 16 persisted totals (8 concepts × primary/alternate).
/// The only implementation of this arithmetic — shared by UpsertCutRecordHandler
/// (compute-at-write) and GetCutRecordHandler's draft path (live compute).
/// No DB access — unit-testable in isolation (design.md Decision 2).
/// </summary>
public static class CutTotalsCalculator
{
    public static CutTotals Compute(
        IEnumerable<(bool IsPositive, decimal BalanceInPrimary)> rows,
        BudgetExecutionSummary summary,
        decimal exchangeRate)
    {
        var rowList = rows as IReadOnlyCollection<(bool IsPositive, decimal BalanceInPrimary)>
            ?? rows.ToList();

        var totalPositive = rowList.Where(r => r.IsPositive).Sum(r => r.BalanceInPrimary);
        var totalNegative = rowList.Where(r => !r.IsPositive).Sum(r => r.BalanceInPrimary);

        var totalBudgeted   = summary.TotalBudgeted;
        var totalRegistered = summary.TotalRegistered;
        var remaining       = summary.Remaining;

        var totalDeudaEnCurso = remaining + totalNegative;
        var totalAvailable    = totalPositive;
        var totalNet          = totalPositive - totalDeudaEnCurso;

        // er <= 0 guard, preserved from the original GetCutRecordHandler behavior.
        var er = exchangeRate > 0 ? exchangeRate : 1m;

        return new CutTotals(
            Round(totalPositive),        Round(totalPositive / er),
            Round(totalNegative),        Round(totalNegative / er),
            Round(totalDeudaEnCurso),    Round(totalDeudaEnCurso / er),
            Round(totalBudgeted),        Round(totalBudgeted / er),
            Round(totalRegistered),      Round(totalRegistered / er),
            Round(remaining),            Round(remaining / er),
            Round(totalAvailable),       Round(totalAvailable / er),
            Round(totalNet),             Round(totalNet / er));
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
