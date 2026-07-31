using MyBudget.Features.Features.CurrentSituation.Shared;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.CurrentSituation;

/// <summary>
/// CS-6: CutTotalsCalculator — pure arithmetic for the 16 persisted totals
/// (8 concepts × primary/alternate). The only implementation of this arithmetic
/// (design.md Decision 2) — unit-tested here in isolation, no DB.
/// </summary>
public sealed class CutTotalsCalculatorTests
{
    [Fact]
    public void Compute_Cs6TableCase_ReturnsExpectedTotals()
    {
        // Spec CS-6 example: A IsPositive=true BP=500, B IsPositive=false BP=200, Remaining=300
        // -> TotalPositive=500, TotalNegative=200, TotalDeudaEnCurso=500 (300 + 200)
        var rows = new (bool IsPositive, decimal BalanceInPrimary)[]
        {
            (true,  500m),
            (false, 200m),
        };
        var summary = new BudgetExecutionSummary(TotalBudgeted: 500m, TotalRegistered: 200m, Remaining: 300m);

        var totals = CutTotalsCalculator.Compute(rows, summary, exchangeRate: 7.8m);

        totals.TotalPositive.ShouldBe(500m);
        totals.TotalNegative.ShouldBe(200m);
        totals.TotalDeudaEnCurso.ShouldBe(500m);
        totals.TotalBudgeted.ShouldBe(500m);
        totals.TotalRegistered.ShouldBe(200m);
        totals.Remaining.ShouldBe(300m);
        totals.TotalAvailable.ShouldBe(500m); // = TotalPositive
        totals.TotalNet.ShouldBe(0m);         // = TotalPositive - TotalDeudaEnCurso = 500 - 500
    }

    [Fact]
    public void Compute_ZeroExchangeRate_UsesOneAsDivisorGuard()
    {
        var rows = new (bool IsPositive, decimal BalanceInPrimary)[] { (true, 780m) };

        var totals = CutTotalsCalculator.Compute(rows, BudgetExecutionSummary.Zero, exchangeRate: 0m);

        // er <= 0 -> divisor 1m, so Alt columns equal their primary counterparts.
        totals.TotalPositiveAlt.ShouldBe(totals.TotalPositive);
        totals.TotalPositiveAlt.ShouldBe(780m);
    }

    [Fact]
    public void Compute_NegativeExchangeRate_UsesOneAsDivisorGuard()
    {
        var rows = new (bool IsPositive, decimal BalanceInPrimary)[] { (true, 780m) };

        var totals = CutTotalsCalculator.Compute(rows, BudgetExecutionSummary.Zero, exchangeRate: -7.8m);

        totals.TotalPositiveAlt.ShouldBe(780m);
    }

    [Fact]
    public void Compute_MoreThanTwoDecimals_RoundsHalfAwayFromZero()
    {
        // 100.125 has a third decimal digit of exactly 5 -> AwayFromZero rounds up to 100.13,
        // not banker's-rounding's 100.12.
        var rows = new (bool IsPositive, decimal BalanceInPrimary)[] { (true, 100.125m) };

        var totals = CutTotalsCalculator.Compute(rows, BudgetExecutionSummary.Zero, exchangeRate: 1m);

        totals.TotalPositive.ShouldBe(100.13m);
        totals.TotalAvailable.ShouldBe(100.13m);
    }

    [Fact]
    public void Compute_NegativeMidpoint_RoundsAwayFromZero()
    {
        // Remaining can go negative (over-budget). -50.125 rounds AwayFromZero to -50.13
        // (more negative), not toward zero's -50.12.
        var summary = new BudgetExecutionSummary(TotalBudgeted: 0m, TotalRegistered: 0m, Remaining: -50.125m);

        var totals = CutTotalsCalculator.Compute(
            Array.Empty<(bool IsPositive, decimal BalanceInPrimary)>(), summary, exchangeRate: 1m);

        totals.Remaining.ShouldBe(-50.13m);
        totals.TotalDeudaEnCurso.ShouldBe(-50.13m); // Remaining + TotalNegative(0)
    }

    [Fact]
    public void Compute_EmptyRows_ReturnsCutTotalsZero()
    {
        var totals = CutTotalsCalculator.Compute(
            Array.Empty<(bool IsPositive, decimal BalanceInPrimary)>(),
            BudgetExecutionSummary.Zero,
            exchangeRate: 7.8m);

        totals.ShouldBe(CutTotals.Zero);
    }
}
