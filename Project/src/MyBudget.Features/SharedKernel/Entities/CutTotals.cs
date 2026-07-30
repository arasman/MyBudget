namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// The 16 financial totals persisted on a CutRecord at save time (CS-6):
/// 8 concepts × primary/alternate currency. Computed by
/// <c>CutTotalsCalculator.Compute</c> and frozen (snapshot semantics) until
/// the cut is explicitly re-saved.
/// </summary>
public sealed record CutTotals(
    decimal TotalPositive,      decimal TotalPositiveAlt,
    decimal TotalNegative,      decimal TotalNegativeAlt,
    decimal TotalDeudaEnCurso,  decimal TotalDeudaEnCursoAlt,
    decimal TotalBudgeted,      decimal TotalBudgetedAlt,
    decimal TotalRegistered,    decimal TotalRegisteredAlt,
    decimal Remaining,          decimal RemainingAlt,
    decimal TotalAvailable,     decimal TotalAvailableAlt,
    decimal TotalNet,           decimal TotalNetAlt)
{
    public static readonly CutTotals Zero = new(
        0m, 0m,
        0m, 0m,
        0m, 0m,
        0m, 0m,
        0m, 0m,
        0m, 0m,
        0m, 0m,
        0m, 0m);
}
