using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetLineRevisionEntityTests
{
    [Fact]
    public void Create_AcceptsCurrencyIdGuid()
    {
        var lineId     = Guid.NewGuid();
        var currencyId = CurrencySeeds.UsdId;

        var revision = BudgetLineRevision.Create(lineId, 500m, currencyId);

        revision.BudgetLineId.ShouldBe(lineId);
        revision.BudgetedAmount.ShouldBe(500m);
        revision.CurrencyId.ShouldBe(currencyId);
    }

    [Fact]
    public void Create_StoresCurrencyId_NotCurrencyString()
    {
        var revision = BudgetLineRevision.Create(Guid.NewGuid(), 100m, CurrencySeeds.GtqId);

        // CurrencyId must be a Guid — not a string field
        revision.CurrencyId.ShouldBeOfType<Guid>();
        revision.CurrencyId.ShouldBe(CurrencySeeds.GtqId);
    }

    [Fact]
    public void Create_SetsRevisedAt_ToUtcNow()
    {
        var before   = DateTimeOffset.UtcNow.AddSeconds(-1);
        var revision = BudgetLineRevision.Create(Guid.NewGuid(), 200m, CurrencySeeds.EurId);
        var after    = DateTimeOffset.UtcNow.AddSeconds(1);

        revision.RevisedAt.ShouldBeGreaterThan(before);
        revision.RevisedAt.ShouldBeLessThan(after);
    }

    [Fact]
    public void Create_NoteIsOptional_DefaultsToNull()
    {
        var revision = BudgetLineRevision.Create(Guid.NewGuid(), 300m, CurrencySeeds.GtqId);

        revision.Note.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNote_StoresNote()
    {
        var revision = BudgetLineRevision.Create(Guid.NewGuid(), 300m, CurrencySeeds.GtqId, "Rent for January");

        revision.Note.ShouldBe("Rent for January");
    }
}
