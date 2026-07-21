using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetLineEntityTests
{
    private static readonly DateOnly Jan1 = new(2025, 1, 1);
    private static readonly DateOnly Dec31 = new(2025, 12, 31);
    private static readonly DateOnly Jun1 = new(2025, 6, 1);
    private static readonly DateOnly May31 = new(2025, 5, 31);
    private static readonly DateOnly Aug31 = new(2025, 8, 31);
    private static readonly DateOnly Sep1 = new(2025, 9, 1);

    private static BudgetLine MakeLine(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        decimal initialAmount = 1500m)
    {
        return BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense,
            startDate ?? Jan1, endDate,
            initialAmount, CurrencySeeds.GtqId);
    }

    // --- Create ---

    [Fact]
    public void Create_InitializesWithOneRevisionCoveringStartAndEndDate()
    {
        var line = MakeLine(Jan1, Dec31, 1500m);

        line.StartDate.ShouldBe(Jan1);
        line.EndDate.ShouldBe(Dec31);
        line.Revisions.Count.ShouldBe(1);

        var rev = line.Revisions.Single();
        rev.ValidFrom.ShouldBe(Jan1);
        rev.ValidTo.ShouldBe(Dec31);
        rev.BudgetedAmount.ShouldBe(1500m);
        rev.CurrencyId.ShouldBe(CurrencySeeds.GtqId);
    }

    [Fact]
    public void Create_WithNullEndDate_RevisionHasNullValidTo()
    {
        var line = MakeLine(Jan1, null);

        line.EndDate.ShouldBeNull();
        line.Revisions.Single().ValidTo.ShouldBeNull();
    }

    [Fact]
    public void Create_AcceptsDisplayOrder()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense,
            Jan1, null,
            1000m, CurrencySeeds.GtqId,
            displayOrder: 3);

        line.DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public void Create_DefaultDisplayOrder_IsZero()
    {
        var line = MakeLine();

        line.DisplayOrder.ShouldBe(0);
    }

    // --- SetDisplayOrder ---

    [Fact]
    public void SetDisplayOrder_UpdatesDisplayOrder()
    {
        var line = MakeLine();
        line.SetDisplayOrder(5);
        line.DisplayOrder.ShouldBe(5);
    }

    // --- SoftDelete / Restore ---

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var line = MakeLine();
        line.SoftDelete();
        line.DeletedAt.ShouldNotBeNull();

        line.Restore();

        line.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_RefreshesUpdatedAt()
    {
        var line = MakeLine();
        line.SoftDelete();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        line.Restore();

        line.UpdatedAt.ShouldNotBeNull();
        line.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- Update ---

    [Fact]
    public void Update_DoesNotAccept_IsRecurring_Parameter()
    {
        // Update(categoryGroupId, categoryId, name, lineType) — 4 params, no isRecurring
        var line = MakeLine();
        line.Update(Guid.NewGuid(), null, "New Name", LineType.LongTermSavings);

        line.Name.ShouldBe("New Name");
        line.LineType.ShouldBe(LineType.LongTermSavings);
    }

    // --- SplitRevision ---

    [Fact]
    public void SplitRevision_MidRange_ProducesThreeGaplessRevisions()
    {
        // Revision: [Jan1, null, 1500 GTQ]
        // Split: Jun1..Aug31 at 2000 GTQ
        // Expected: [Jan1, May31, 1500], [Jun1, Aug31, 2000], [Sep1, null, 1500]
        var line = MakeLine(Jan1, null, 1500m);

        line.SplitRevision(Jun1, Aug31, 2000m, CurrencySeeds.GtqId);

        line.Revisions.Count.ShouldBe(3);

        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();

        sorted[0].ValidFrom.ShouldBe(Jan1);
        sorted[0].ValidTo.ShouldBe(May31);
        sorted[0].BudgetedAmount.ShouldBe(1500m);

        sorted[1].ValidFrom.ShouldBe(Jun1);
        sorted[1].ValidTo.ShouldBe(Aug31);
        sorted[1].BudgetedAmount.ShouldBe(2000m);

        sorted[2].ValidFrom.ShouldBe(Sep1);
        sorted[2].ValidTo.ShouldBeNull();
        sorted[2].BudgetedAmount.ShouldBe(1500m);
    }

    [Fact]
    public void SplitRevision_AtExactBoundary_OverwritesInPlace_StaysOneRevision()
    {
        // Edge Case B: newValidFrom == enclosing.ValidFrom -> overwrite in-place
        var line = MakeLine(Jan1, null, 1500m);

        line.SplitRevision(Jan1, null, 2000m, CurrencySeeds.UsdId);

        line.Revisions.Count.ShouldBe(1);
        var rev = line.Revisions.Single();
        rev.BudgetedAmount.ShouldBe(2000m);
        rev.CurrencyId.ShouldBe(CurrencySeeds.UsdId);
        rev.ValidFrom.ShouldBe(Jan1);
        rev.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void SplitRevision_AtStartOfOpenEndedRevision_ProducesTwoRevisions_LastHasNullValidTo()
    {
        // Split open-ended: Jun1..null at 2000
        // Expected: [Jan1, May31, 1500], [Jun1, null, 2000]
        var line = MakeLine(Jan1, null, 1500m);

        line.SplitRevision(Jun1, null, 2000m, CurrencySeeds.GtqId);

        line.Revisions.Count.ShouldBe(2);

        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();

        sorted[0].ValidFrom.ShouldBe(Jan1);
        sorted[0].ValidTo.ShouldBe(May31);

        sorted[1].ValidFrom.ShouldBe(Jun1);
        sorted[1].ValidTo.ShouldBeNull();
        sorted[1].BudgetedAmount.ShouldBe(2000m);
    }

    [Fact]
    public void SplitRevision_NoEnclosingRevision_ThrowsInvalidOperationException()
    {
        // BudgetLine [Jan1, Jun30, 1500]; try split at Aug1 — no enclosing
        var line = MakeLine(Jan1, new DateOnly(2025, 6, 30), 1500m);

        Should.Throw<InvalidOperationException>(() =>
            line.SplitRevision(new DateOnly(2025, 8, 1), null, 2000m, CurrencySeeds.GtqId));
    }
}
