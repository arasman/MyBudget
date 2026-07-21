using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetLineRevisionEntityTests
{
    [Fact]
    public void Create_SetsValidFromAndValidTo_Correctly()
    {
        var validFrom = new DateOnly(2025, 1, 1);
        var validTo   = new DateOnly(2025, 12, 31);

        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            500m, CurrencySeeds.UsdId,
            validFrom, validTo);

        revision.ValidFrom.ShouldBe(validFrom);
        revision.ValidTo.ShouldBe(validTo);
    }

    [Fact]
    public void Create_WithNullValidTo_SetsValidToNull()
    {
        var validFrom = new DateOnly(2025, 1, 1);

        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            500m, CurrencySeeds.GtqId,
            validFrom, null);

        revision.ValidFrom.ShouldBe(validFrom);
        revision.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void Create_AcceptsCurrencyIdGuid()
    {
        var lineId     = Guid.NewGuid();
        var currencyId = CurrencySeeds.UsdId;
        var validFrom  = new DateOnly(2025, 6, 1);

        var revision = BudgetLineRevision.Create(Guid.NewGuid(), lineId, 500m, currencyId, validFrom, null);

        revision.BudgetLineId.ShouldBe(lineId);
        revision.BudgetedAmount.ShouldBe(500m);
        revision.CurrencyId.ShouldBe(currencyId);
    }

    [Fact]
    public void Create_StoresCurrencyId_NotCurrencyString()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, CurrencySeeds.GtqId,
            new DateOnly(2025, 1, 1), null);

        revision.CurrencyId.ShouldBeOfType<Guid>();
        revision.CurrencyId.ShouldBe(CurrencySeeds.GtqId);
    }

    [Fact]
    public void Create_NoteIsOptional_DefaultsToNull()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            300m, CurrencySeeds.GtqId,
            new DateOnly(2025, 1, 1), null);

        revision.Note.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNote_StoresNote()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            300m, CurrencySeeds.GtqId,
            new DateOnly(2025, 1, 1), null,
            "Rent for January");

        revision.Note.ShouldBe("Rent for January");
    }

    [Fact]
    public void SetValidTo_MutatesValidTo()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            500m, CurrencySeeds.UsdId,
            new DateOnly(2025, 1, 1), null);

        var newValidTo = new DateOnly(2025, 5, 31);
        revision.SetValidTo(newValidTo);

        revision.ValidTo.ShouldBe(newValidTo);
    }

    [Fact]
    public void SetValidTo_CanSetToNull()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            500m, CurrencySeeds.UsdId,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        revision.SetValidTo(null);

        revision.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void SetAmount_MutatesBudgetedAmountAndCurrencyId()
    {
        var revision = BudgetLineRevision.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            500m, CurrencySeeds.UsdId,
            new DateOnly(2025, 1, 1), null);

        revision.SetAmount(1200m, CurrencySeeds.GtqId);

        revision.BudgetedAmount.ShouldBe(1200m);
        revision.CurrencyId.ShouldBe(CurrencySeeds.GtqId);
    }
}
