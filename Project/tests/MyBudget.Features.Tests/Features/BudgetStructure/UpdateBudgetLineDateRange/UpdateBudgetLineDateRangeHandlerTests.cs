using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineDateRange;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLineDateRange;

/// <summary>
/// Unit tests for UpdateBudgetLineDateRangeHandler.
/// Focus: the SyncValidFrom path — when a line has exactly one revision whose ValidFrom
/// equals the current StartDate, moving StartDate forward syncs the revision's ValidFrom
/// instead of rejecting with RANGE_WOULD_ORPHAN_REVISION.
/// </summary>
public sealed class UpdateBudgetLineDateRangeHandlerTests : IDisposable
{
    private readonly string _connectionString;
    private readonly AppDbContext _seedDb;

    public UpdateBudgetLineDateRangeHandlerTests()
    {
        var dbName = $"handler-update-daterange-{Guid.NewGuid():N}";
        _connectionString = $"DataSource={dbName};Mode=Memory;Cache=Shared";

        _seedDb = CreateContext();
        _seedDb.Database.OpenConnection();
        _seedDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _seedDb.Dispose();
    }

    private AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(opts);
    }

    private async Task<(Guid budgetId, Guid lineId)> SeedLineWithSingleRevisionAsync(
        DateOnly startDate, DateOnly? endDate = null)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_seedDb);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _seedDb.CategoryGroups.Add(group);
        await _seedDb.SaveChangesAsync();

        var line = BudgetLine.Create(
            budgetId, group.Id, null, "Rent", LineType.Expense,
            startDate, endDate, 1000m, CurrencySeeds.GtqId);
        _seedDb.BudgetLines.Add(line);
        await _seedDb.SaveChangesAsync();

        return (budgetId, line.Id);
    }

    /// <summary>
    /// REQ-BL-DATERANGE-1 SyncValidFrom path:
    /// Single-revision line where revision.ValidFrom == line.StartDate.
    /// Moving StartDate forward by 5 days should sync the revision's ValidFrom to the new StartDate
    /// instead of rejecting with RANGE_WOULD_ORPHAN_REVISION.
    /// </summary>
    [Fact]
    public async Task MoveStartDateForward_SingleRevisionMatchingStartDate_SyncsValidFrom()
    {
        var startDate = new DateOnly(2025, 1, 1);
        var (budgetId, lineId) = await SeedLineWithSingleRevisionAsync(startDate);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineDateRangeHandler(handlerDb);

        var newStartDate = startDate.AddDays(5);
        var cmd = new UpdateBudgetLineDateRangeCommand(budgetId, lineId, newStartDate, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Verify the revision's ValidFrom was synced to the new StartDate
        await using var verifyDb = CreateContext();
        var revision = await verifyDb.BudgetLineRevisions
            .FirstAsync(r => r.BudgetLineId == lineId);
        revision.ValidFrom.ShouldBe(newStartDate);

        // Verify the line's StartDate was also updated
        var line = await verifyDb.BudgetLines.FirstAsync(l => l.Id == lineId);
        line.StartDate.ShouldBe(newStartDate);
    }

    /// <summary>
    /// When there is more than one revision, moving StartDate forward should NOT sync
    /// the original revision's ValidFrom — instead the domain guard fires if the revision
    /// would be orphaned.
    /// </summary>
    [Fact]
    public async Task MoveStartDateForward_MultipleRevisions_DoesNotSyncAndFails()
    {
        var startDate = new DateOnly(2025, 1, 1);
        var (budgetId, lineId) = await SeedLineWithSingleRevisionAsync(startDate);

        // Add a second revision directly via a fresh context to avoid EF identity-map conflicts.
        // The fresh context shares the same in-memory SQLite connection string.
        await using (var splitDb = CreateContext())
        {
            // Trim the existing revision ValidTo and insert a second revision directly.
            // Using ExecuteUpdate avoids EF tracking conflicts from the audit interceptor.
            var splitAt = startDate.AddDays(30);

            var existing = await splitDb.BudgetLineRevisions
                .FirstAsync(r => r.BudgetLineId == lineId);
            existing.SetValidTo(splitAt.AddDays(-1));

            var secondRevision = BudgetLineRevision.Create(
                budgetId, lineId, 2000m, CurrencySeeds.GtqId, splitAt, null);
            splitDb.BudgetLineRevisions.Add(secondRevision);
            await splitDb.SaveChangesAsync();
        }

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineDateRangeHandler(handlerDb);

        // With two revisions, the sync path does NOT fire.
        // Moving StartDate forward by 5 days: revision[0].ValidFrom = startDate < newStartDate
        // → domain guard fires → RANGE_WOULD_ORPHAN_REVISION (422)
        var newStartDate = startDate.AddDays(5);
        var cmd = new UpdateBudgetLineDateRangeCommand(budgetId, lineId, newStartDate, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("RANGE_WOULD_ORPHAN_REVISION");
    }

    /// <summary>
    /// Line not found → BUDGET_LINE_NOT_FOUND.
    /// </summary>
    [Fact]
    public async Task UpdateDateRange_LineNotFound_Returns_BUDGET_LINE_NOT_FOUND()
    {
        var startDate = new DateOnly(2025, 1, 1);
        var (budgetId, _) = await SeedLineWithSingleRevisionAsync(startDate);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineDateRangeHandler(handlerDb);

        var cmd = new UpdateBudgetLineDateRangeCommand(budgetId, Guid.NewGuid(), startDate, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }

    /// <summary>
    /// Keeping StartDate the same as the revision's ValidFrom does NOT trigger SyncValidFrom
    /// (condition: original.ValidFrom != cmd.StartDate). Should succeed normally.
    /// </summary>
    [Fact]
    public async Task MoveStartDateSame_SingleRevision_Succeeds_WithoutSync()
    {
        var startDate = new DateOnly(2025, 1, 1);
        var (budgetId, lineId) = await SeedLineWithSingleRevisionAsync(startDate);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineDateRangeHandler(handlerDb);

        // Same StartDate — no change needed, no sync triggered
        var cmd = new UpdateBudgetLineDateRangeCommand(budgetId, lineId, startDate, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await using var verifyDb = CreateContext();
        var revision = await verifyDb.BudgetLineRevisions
            .FirstAsync(r => r.BudgetLineId == lineId);
        // ValidFrom unchanged
        revision.ValidFrom.ShouldBe(startDate);
    }
}
