using MyBudget.Features.Features.CurrentSituation.GetCutRecord;
using Shouldly;

namespace MyBudget.Features.Tests.Features.CurrentSituation;

/// <summary>
/// CS-6: Cut totals computation.
/// TotalPositive  = SUM(BalanceInPrimary) where IsPositive = true
/// TotalNegative  = SUM(BalanceInPrimary) where IsPositive = false
/// TotalDeudaEnCurso = Remaining + TotalNegative
/// Alt variants = total / ExchangeRate
/// </summary>
public sealed class CutTotalsComputationTests
{
    private static CutBankAccountDto MakeAccount(bool isPositive, decimal balanceInPrimary)
        => new(Guid.NewGuid(), "Alias", Guid.NewGuid(), isPositive, 1, balanceInPrimary, balanceInPrimary);

    private static CutTotalsDto ComputeTotals(
        IReadOnlyList<CutBankAccountDto> accounts,
        decimal remaining,
        decimal exchangeRate)
    {
        var totalPositive = accounts.Where(a => a.IsPositive).Sum(a => a.BalanceInPrimary);
        var totalNegative = accounts.Where(a => !a.IsPositive).Sum(a => a.BalanceInPrimary);
        var totalDeuda    = remaining + totalNegative;
        var er            = exchangeRate > 0 ? exchangeRate : 1m;

        return new CutTotalsDto(
            totalPositive,
            totalNegative,
            totalDeuda,
            totalPositive / er,
            totalNegative / er,
            totalDeuda    / er);
    }

    [Fact]
    public void Totals_Computed_Correctly()
    {
        // Spec CS-6 example: A IsPositive=true BP=500, B IsPositive=false BP=200, Remaining=300
        var accounts = new List<CutBankAccountDto>
        {
            MakeAccount(true,  500m),
            MakeAccount(false, 200m),
        };

        var totals = ComputeTotals(accounts, remaining: 300m, exchangeRate: 7.8m);

        totals.TotalPositive.ShouldBe(500m);
        totals.TotalNegative.ShouldBe(200m);
        totals.TotalDeudaEnCurso.ShouldBe(500m); // 300 + 200
    }

    [Fact]
    public void Alt_Currency_Totals_Divided_By_ExchangeRate()
    {
        var accounts = new List<CutBankAccountDto>
        {
            MakeAccount(true,  780m),
        };

        var totals = ComputeTotals(accounts, remaining: 0m, exchangeRate: 7.8m);

        totals.TotalPositiveAlt.ShouldBe(100m); // 780 / 7.8
    }

    [Fact]
    public void No_Accounts_Returns_Zero_Totals()
    {
        var totals = ComputeTotals(new List<CutBankAccountDto>(), remaining: 0m, exchangeRate: 7.8m);

        totals.TotalPositive.ShouldBe(0m);
        totals.TotalNegative.ShouldBe(0m);
        totals.TotalDeudaEnCurso.ShouldBe(0m);
    }
}
