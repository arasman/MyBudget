using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

public sealed class UpdateBudgetLineHandlerTests : IDisposable
{
    // Use a shared SQLite file-based connection string per test instance
    // so that seeding DbContext and handler DbContext share the same data
    // but do NOT share the same EF change tracker (avoids identity map conflicts).
    private readonly string _connectionString;
    private readonly AppDbContext _seedDb;

    public UpdateBudgetLineHandlerTests()
    {
        // Unique in-memory SQLite DB per test class instance
        var dbName = $"handler-update-{Guid.NewGuid():N}";
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

    private async Task<(Guid budgetId, Guid periodId, Guid lineId, Guid groupId)> SeedLineAsync(bool isClosed = false)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_seedDb);

        var cycle = Cycle.Create(budgetId, "Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);
        _seedDb.Cycles.Add(cycle);
        await _seedDb.SaveChangesAsync();

        var period = Period.Create(cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        if (isClosed) period.SetClosed(true);
        _seedDb.Periods.Add(period);
        await _seedDb.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _seedDb.CategoryGroups.Add(group);
        await _seedDb.SaveChangesAsync();

        var line = BudgetLine.Create(period.Id, group.Id, null, "Rent", LineType.Expense, true);
        _seedDb.BudgetLines.Add(line);
        await _seedDb.SaveChangesAsync();

        var revision = BudgetLineRevision.Create(line.Id, 1000m, CurrencySeeds.GtqId);
        _seedDb.BudgetLineRevisions.Add(revision);
        await _seedDb.SaveChangesAsync();

        return (budgetId, period.Id, line.Id, group.Id);
    }

    [Fact]
    public async Task ClosedPeriod_Returns_PERIOD_CLOSED()
    {
        var (budgetId, periodId, lineId, groupId) = await SeedLineAsync(isClosed: true);

        // Use a FRESH context for the handler — no shared tracking with seed context
        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        var cmd = new UpdateBudgetLineCommand(
            budgetId, periodId, lineId, groupId, null,
            "Rent Updated", LineType.Expense, true, 2000m, CurrencySeeds.GtqId);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task OpenPeriod_CreatesNewRevisionWithoutModifyingExisting()
    {
        var (budgetId, periodId, lineId, groupId) = await SeedLineAsync(isClosed: false);

        // Read original revision from seed context
        var originalRevision = await _seedDb.BudgetLineRevisions
            .FirstAsync(r => r.BudgetLineId == lineId);
        var originalAmount = originalRevision.BudgetedAmount;
        var originalRevisedAt = originalRevision.RevisedAt;

        // Use a FRESH context for the handler — no shared tracking with seed context
        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        var cmd = new UpdateBudgetLineCommand(
            budgetId, periodId, lineId, groupId, null,
            "Rent Updated", LineType.LongTermSavings, false, 2000m, CurrencySeeds.UsdId);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Verify via the seed context (which has no knowledge of handler changes yet — reload)
        await _seedDb.Entry(originalRevision).ReloadAsync();

        // Check revisions count via a fresh query context
        await using var verifyDb = CreateContext();
        var revisionsAfter = await verifyDb.BudgetLineRevisions
            .Where(r => r.BudgetLineId == lineId)
            .ToListAsync();
        revisionsAfter.Count.ShouldBe(2);

        // Original revision is byte-for-byte unchanged
        var reloadedOriginal = revisionsAfter.First(r => r.BudgetedAmount == originalAmount);
        reloadedOriginal.RevisedAt.ShouldBe(originalRevisedAt);

        // New revision has updated values
        var newRevision = revisionsAfter.First(r => r.BudgetedAmount == 2000m);
        newRevision.CurrencyId.ShouldBe(CurrencySeeds.UsdId);
    }
}
