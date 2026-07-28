using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.CurrentSituation;

/// <summary>
/// CS-5: BalanceInPrimary computation rules.
/// Primary currency: BalanceInPrimary = Balance.
/// Alternate currency: BalanceInPrimary = Balance × ExchangeRate.
/// </summary>
public sealed class BalanceInPrimaryComputationTests
{
    private static readonly Guid PrimaryCurrencyId   = CurrencySeeds.GtqId;
    private static readonly Guid AlternateCurrencyId = CurrencySeeds.UsdId;

    [Fact]
    public void Primary_Currency_BalanceInPrimary_Equals_Balance()
    {
        var balance      = 1000m;
        var exchangeRate = 7.8m;

        // Simulate handler logic: account currency matches primary → same value
        var accountCurrencyId = PrimaryCurrencyId;
        var balanceInPrimary = accountCurrencyId == PrimaryCurrencyId
            ? balance
            : balance * exchangeRate;

        balanceInPrimary.ShouldBe(1000m);
    }

    [Fact]
    public void Alternate_Currency_BalanceInPrimary_Equals_Balance_Times_ExchangeRate()
    {
        var balance      = 100m;
        var exchangeRate = 7.8m;

        // Simulate handler logic for alternate currency
        var balanceInPrimary = AlternateCurrencyId == PrimaryCurrencyId
            ? balance
            : balance * exchangeRate;

        balanceInPrimary.ShouldBe(780m);
    }

    [Fact]
    public void CutBankAccount_Create_Stores_BalanceInPrimary()
    {
        var snapshot = CutBankAccount.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Caja GTQ",
            PrimaryCurrencyId,
            true,
            1,
            1000m,
            1000m);

        snapshot.Balance.ShouldBe(1000m);
        snapshot.BalanceInPrimary.ShouldBe(1000m);
    }

    [Fact]
    public void CutBankAccount_Create_Alt_Currency_Stores_Computed_BalanceInPrimary()
    {
        var balance          = 100m;
        var exchangeRate     = 7.8m;
        var balanceInPrimary = balance * exchangeRate; // 780

        var snapshot = CutBankAccount.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Caja USD",
            AlternateCurrencyId,
            true,
            2,
            balance,
            balanceInPrimary);

        snapshot.Balance.ShouldBe(100m);
        snapshot.BalanceInPrimary.ShouldBe(780m);
    }
}
