using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class CycleEntityTests
{
    private static Cycle BuildCycle(
        Guid?    defaultCurrencyId   = null,
        Guid?    alternateCurrencyId = null,
        decimal? exchangeRate        = null)
    {
        return Cycle.Create(
            Guid.NewGuid(),
            "Test Cycle",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31),
            defaultCurrencyId ?? CurrencySeeds.GtqId,
            alternateCurrencyId,
            exchangeRate);
    }

    [Fact]
    public void Create_SetsCurrencyFields()
    {
        var cycle = BuildCycle(
            defaultCurrencyId:   CurrencySeeds.GtqId,
            alternateCurrencyId: CurrencySeeds.UsdId,
            exchangeRate:        7.5m);

        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.GtqId);
        cycle.AlternateCurrencyId.ShouldBe(CurrencySeeds.UsdId);
        cycle.ExchangeRate.ShouldBe(7.5m);
    }

    [Fact]
    public void Create_WithoutAlternateCurrency_LeavesNullFields()
    {
        var cycle = BuildCycle(defaultCurrencyId: CurrencySeeds.GtqId);

        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.GtqId);
        cycle.AlternateCurrencyId.ShouldBeNull();
        cycle.ExchangeRate.ShouldBeNull();
    }

    [Fact]
    public void Update_UpdatesCurrencyFields()
    {
        var cycle = BuildCycle(defaultCurrencyId: CurrencySeeds.GtqId);

        cycle.Update(
            "Updated",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31),
            CurrencySeeds.UsdId,
            CurrencySeeds.EurId,
            1.1m);

        cycle.DefaultCurrencyId.ShouldBe(CurrencySeeds.UsdId);
        cycle.AlternateCurrencyId.ShouldBe(CurrencySeeds.EurId);
        cycle.ExchangeRate.ShouldBe(1.1m);
    }

    [Fact]
    public void Restore_ClearsDeletedAt_And_RefreshesUpdatedAt()
    {
        var cycle = BuildCycle();
        cycle.SoftDelete();
        cycle.DeletedAt.ShouldNotBeNull();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        cycle.Restore();

        cycle.DeletedAt.ShouldBeNull();
        cycle.UpdatedAt.ShouldNotBeNull();
        cycle.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }
}
