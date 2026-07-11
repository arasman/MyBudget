using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel;

public sealed class CurrencyEntityTests
{
    [Fact]
    public void CurrencySeeds_GtqId_MatchesExpectedGuid()
    {
        CurrencySeeds.GtqId.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public void CurrencySeeds_UsdId_MatchesExpectedGuid()
    {
        CurrencySeeds.UsdId.ShouldBe(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Fact]
    public void CurrencySeeds_EurId_MatchesExpectedGuid()
    {
        CurrencySeeds.EurId.ShouldBe(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    }

    [Fact]
    public void CurrencySeeds_AllIds_AreDistinct()
    {
        var ids = new[] { CurrencySeeds.GtqId, CurrencySeeds.UsdId, CurrencySeeds.EurId };
        ids.Distinct().Count().ShouldBe(3);
    }
}
