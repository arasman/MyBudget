using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineRevision;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLineRevision;

/// <summary>
/// Unit tests for UpdateBudgetLineRevisionHandler.
/// Covers: happy path (amount + note), REVISION_NOT_FOUND (wrong budget/line),
/// REVISION_AMOUNT_INVALID (negative amount), and the boundary case amount = 0 (allowed).
/// </summary>
public sealed class UpdateBudgetLineRevisionHandlerTests : IDisposable
{
    private readonly string _connectionString;
    private readonly AppDbContext _seedDb;

    public UpdateBudgetLineRevisionHandlerTests()
    {
        var dbName = $"handler-update-revision-{Guid.NewGuid():N}";
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

    /// <summary>
    /// Seeds the minimum entity chain: Budget → CategoryGroup → BudgetLine (with initial revision).
    /// Returns budgetId, lineId, and the initial revisionId.
    /// </summary>
    private async Task<(Guid budgetId, Guid lineId, Guid revisionId)> SeedLineAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_seedDb);

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _seedDb.CategoryGroups.Add(group);
        await _seedDb.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var line = BudgetLine.Create(
            budgetId, group.Id, null, "Rent", LineType.Expense,
            startDate, null, 1000m, CurrencySeeds.GtqId);
        _seedDb.BudgetLines.Add(line);
        await _seedDb.SaveChangesAsync();

        // Load the auto-created initial revision
        var revision = await _seedDb.BudgetLineRevisions
            .FirstAsync(r => r.BudgetLineId == line.Id);

        return (budgetId, line.Id, revision.Id);
    }

    [Fact]
    public async Task UpdateRevision_HappyPath_UpdatesAmountAndNote()
    {
        var (budgetId, lineId, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, lineId, revisionId, 1500m, "Updated note");
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Verify persisted values
        await using var verifyDb = CreateContext();
        var revision = await verifyDb.BudgetLineRevisions.FirstAsync(r => r.Id == revisionId);
        revision.BudgetedAmount.ShouldBe(1500m);
        revision.Note.ShouldBe("Updated note");
    }

    [Fact]
    public async Task UpdateRevision_ClearsNote_WhenNoteIsNull()
    {
        var (budgetId, lineId, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, lineId, revisionId, 1000m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await using var verifyDb = CreateContext();
        var revision = await verifyDb.BudgetLineRevisions.FirstAsync(r => r.Id == revisionId);
        revision.Note.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateRevision_WrongRevisionId_Returns_REVISION_NOT_FOUND()
    {
        var (budgetId, lineId, _) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, lineId, Guid.NewGuid(), 1500m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REVISION_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateRevision_WrongLineId_Returns_REVISION_NOT_FOUND()
    {
        var (budgetId, _, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        // RevisionId exists but LineId is wrong — should not find it
        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, Guid.NewGuid(), revisionId, 1500m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REVISION_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateRevision_WrongBudgetId_Returns_REVISION_NOT_FOUND()
    {
        var (_, lineId, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(Guid.NewGuid(), lineId, revisionId, 1500m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REVISION_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateRevision_NegativeAmount_Returns_REVISION_AMOUNT_INVALID()
    {
        var (budgetId, lineId, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, lineId, revisionId, -1m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("REVISION_AMOUNT_INVALID");
    }

    /// <summary>
    /// Boundary: amount = 0 is valid (changed from &lt;= 0 to &lt; 0 in the fix branch).
    /// </summary>
    [Fact]
    public async Task UpdateRevision_AmountZero_Succeeds()
    {
        var (budgetId, lineId, revisionId) = await SeedLineAsync();

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineRevisionHandler(handlerDb);

        var cmd = new UpdateBudgetLineRevisionCommand(budgetId, lineId, revisionId, 0m, null);
        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await using var verifyDb = CreateContext();
        var revision = await verifyDb.BudgetLineRevisions.FirstAsync(r => r.Id == revisionId);
        revision.BudgetedAmount.ShouldBe(0m);
    }
}
