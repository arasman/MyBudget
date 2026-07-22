using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

/// <summary>
/// Unit tests for BudgetLine.DeleteRevision.
/// Covers: middle-revision gapless repair, last-revision open-ended result,
/// original-revision block, active-execution block, soft-deleted execution pass.
/// REQ-BLR-03
/// </summary>
public sealed class BudgetLineDeleteRevisionTests
{
    private static readonly DateOnly Jan1  = new(2025, 1, 1);
    private static readonly DateOnly Mar31 = new(2025, 3, 31);
    private static readonly DateOnly Apr1  = new(2025, 4, 1);
    private static readonly DateOnly Jun30 = new(2025, 6, 30);
    private static readonly DateOnly Jul1  = new(2025, 7, 1);
    private static readonly DateOnly Sep30 = new(2025, 9, 30);

    /// <summary>
    /// Creates a BudgetLine with a single open-ended revision starting at Jan1.
    /// </summary>
    private static BudgetLine MakeLine(DateOnly? endDate = null)
    {
        return BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "Test Line", LineType.Expense,
            Jan1, endDate,
            1000m, CurrencySeeds.GtqId);
    }

    /// <summary>
    /// Builds a line with three revisions: [Jan1, Mar31], [Apr1, Jun30], [Jul1, null].
    /// Uses SplitRevision to produce the chain.
    /// </summary>
    private static BudgetLine MakeThreeRevisionLine()
    {
        var line = MakeLine();
        // Split at Apr1 with ValidTo Jun30 → produces [Jan1,Mar31], [Apr1,Jun30], [Jul1,null]
        line.SplitRevision(Apr1, Jun30, 2000m, CurrencySeeds.UsdId);
        return line;
    }

    // ── Middle revision: gapless repair ─────────────────────────────────────

    [Fact]
    public void DeleteRevision_MiddleRevision_ReturnedRevisionIsTheDeletedOne()
    {
        // Arrange: [Jan1,Mar31,1000 GTQ], [Apr1,Jun30,2000 USD], [Jul1,null,1000 GTQ]
        var line = MakeThreeRevisionLine();
        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        var middleId = sorted[1].Id; // Apr1..Jun30

        var removed = line.DeleteRevision(middleId, hasActiveExecutions: false);

        removed.Id.ShouldBe(middleId);
    }

    [Fact]
    public void DeleteRevision_MiddleRevision_PredecessorValidToExtendedToMiddleValidTo()
    {
        // Arrange: [Jan1,Mar31], [Apr1,Jun30], [Jul1,null]
        var line = MakeThreeRevisionLine();
        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        var middleId = sorted[1].Id;

        line.DeleteRevision(middleId, hasActiveExecutions: false);

        var after = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        after.Count.ShouldBe(2);
        // Predecessor absorbs middle's range
        after[0].ValidFrom.ShouldBe(Jan1);
        after[0].ValidTo.ShouldBe(Jun30);
        // Successor unchanged
        after[1].ValidFrom.ShouldBe(Jul1);
        after[1].ValidTo.ShouldBeNull();
    }

    // ── Last revision: open-ended result ────────────────────────────────────

    [Fact]
    public void DeleteRevision_LastRevision_PredecessorBecomesOpenEnded()
    {
        // Arrange: [Jan1,Mar31], [Apr1,Jun30], [Jul1,null] → delete last (Jul1,null)
        var line = MakeThreeRevisionLine();
        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        var lastId = sorted[2].Id; // Jul1..null

        var removed = line.DeleteRevision(lastId, hasActiveExecutions: false);

        removed.Id.ShouldBe(lastId);
        line.Revisions.Count.ShouldBe(2);

        var after = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        after[0].ValidFrom.ShouldBe(Jan1);
        after[0].ValidTo.ShouldBe(Mar31);
        after[1].ValidFrom.ShouldBe(Apr1);
        after[1].ValidTo.ShouldBeNull(); // predecessor of last becomes open-ended
    }

    // ── Original revision block ──────────────────────────────────────────────

    [Fact]
    public void DeleteRevision_OriginalRevision_ThrowsCannotDeleteOriginalRevision()
    {
        // A line with a single revision — that revision IS the original
        var line = MakeLine();
        var originalId = line.Revisions.Single().Id;

        var ex = Should.Throw<InvalidOperationException>(() =>
            line.DeleteRevision(originalId, hasActiveExecutions: false));

        ex.Message.ShouldContain("CANNOT_DELETE_ORIGINAL_REVISION");
    }

    [Fact]
    public void DeleteRevision_EarliestRevisionInChain_ThrowsCannotDeleteOriginalRevision()
    {
        // Three revisions; the original is the one with the earliest ValidFrom
        var line = MakeThreeRevisionLine();
        var originalId = line.Revisions.OrderBy(r => r.ValidFrom).First().Id;

        var ex = Should.Throw<InvalidOperationException>(() =>
            line.DeleteRevision(originalId, hasActiveExecutions: false));

        ex.Message.ShouldContain("CANNOT_DELETE_ORIGINAL_REVISION");
    }

    // ── Active execution block ───────────────────────────────────────────────

    [Fact]
    public void DeleteRevision_WithActiveExecutions_ThrowsRevisionHasActiveExecutions()
    {
        var line = MakeThreeRevisionLine();
        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        var middleId = sorted[1].Id;

        var ex = Should.Throw<InvalidOperationException>(() =>
            line.DeleteRevision(middleId, hasActiveExecutions: true));

        ex.Message.ShouldContain("REVISION_HAS_ACTIVE_EXECUTIONS");
    }

    // ── Soft-deleted execution passes ────────────────────────────────────────

    [Fact]
    public void DeleteRevision_SoftDeletedExecutionsOnly_Succeeds()
    {
        // hasActiveExecutions = false means only soft-deleted executions exist (or none)
        var line = MakeThreeRevisionLine();
        var sorted = line.Revisions.OrderBy(r => r.ValidFrom).ToList();
        var middleId = sorted[1].Id;

        // Should not throw
        var removed = line.DeleteRevision(middleId, hasActiveExecutions: false);

        removed.ShouldNotBeNull();
        removed.Id.ShouldBe(middleId);
    }

    // ── Not found ────────────────────────────────────────────────────────────

    [Fact]
    public void DeleteRevision_UnknownRevisionId_ThrowsInvalidOperationException()
    {
        var line = MakeLine();

        Should.Throw<InvalidOperationException>(() =>
            line.DeleteRevision(Guid.NewGuid(), hasActiveExecutions: false));
    }
}
