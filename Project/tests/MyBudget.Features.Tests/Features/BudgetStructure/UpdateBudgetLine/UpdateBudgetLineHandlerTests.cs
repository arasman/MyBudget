using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

public sealed class UpdateBudgetLineHandlerTests : IDisposable
{
    private readonly string _connectionString;
    private readonly AppDbContext _seedDb;

    public UpdateBudgetLineHandlerTests()
    {
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

    private async Task<(Guid budgetId, Guid lineId, Guid groupId)> SeedLineAsync(bool isClosed = false)
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_seedDb);

        var cycle = Cycle.Create(budgetId, "Cycle",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
            CurrencySeeds.GtqId);
        _seedDb.Cycles.Add(cycle);
        await _seedDb.SaveChangesAsync();

        var period = Period.Create(budgetId, cycle.Id, "January", 1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        if (isClosed) period.SetClosed(true);
        _seedDb.Periods.Add(period);
        await _seedDb.SaveChangesAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _seedDb.CategoryGroups.Add(group);
        await _seedDb.SaveChangesAsync();

        // TODO PR4: update to new BudgetLine.Create signature
        var line = BudgetLine.Create(budgetId, group.Id, null, "Rent", LineType.Expense,
            DateOnly.FromDateTime(DateTime.UtcNow), null, 1000m, CurrencySeeds.GtqId);
        _seedDb.BudgetLines.Add(line);
        await _seedDb.SaveChangesAsync();

        return (budgetId, line.Id, group.Id);
    }

    [Fact]
    public async Task MetadataUpdate_Succeeds()
    {
        var (budgetId, lineId, groupId) = await SeedLineAsync(isClosed: false);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        var cmd = new UpdateBudgetLineCommand(
            budgetId, lineId, groupId, null,
            "Rent Updated", LineType.Expense,
            null, null, null, null);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task LineNotFound_Returns_BUDGET_LINE_NOT_FOUND()
    {
        var (budgetId, _, groupId) = await SeedLineAsync(isClosed: false);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        var cmd = new UpdateBudgetLineCommand(
            budgetId, Guid.NewGuid(), groupId, null,
            "Rent Updated", LineType.Expense,
            null, null, null, null);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_LINE_NOT_FOUND");
    }

    [Fact]
    public async Task RevisionSplit_WithClosedPeriodCoveringValidFrom_Returns_PERIOD_CLOSED()
    {
        // isClosed=true seeds the period as closed, covering UtcNow..UtcNow+30 days
        var (budgetId, lineId, groupId) = await SeedLineAsync(isClosed: true);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        // ValidFrom = today falls inside the closed period
        var validFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        var cmd = new UpdateBudgetLineCommand(
            budgetId, lineId, groupId, null,
            "Rent", LineType.Expense,
            validFrom, null, 2000m, CurrencySeeds.GtqId);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("PERIOD_CLOSED");
    }

    [Fact]
    public async Task RevisionSplit_WithOpenPeriod_Succeeds()
    {
        var (budgetId, lineId, groupId) = await SeedLineAsync(isClosed: false);

        await using var handlerDb = CreateContext();
        var sut = new UpdateBudgetLineHandler(handlerDb);

        var validFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        var cmd = new UpdateBudgetLineCommand(
            budgetId, lineId, groupId, null,
            "Rent", LineType.Expense,
            validFrom, null, 2000m, CurrencySeeds.GtqId);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
