using Microsoft.EntityFrameworkCore;
using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

// TODO PR4: full rewrite — tests for budget-scoped create with StartDate/EndDate/InitialRevision
public sealed class CreateBudgetLineHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateBudgetLineHandler _sut;

    public CreateBudgetLineHandlerTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateBudgetLineHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid budgetId, Guid groupId)> SeedBudgetWithGroupAsync()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);
        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();
        return (budgetId, group.Id);
    }

    [Fact]
    public async Task HappyPath_CreatesLineWithInitialRevision()
    {
        var (budgetId, groupId) = await SeedBudgetWithGroupAsync();
        var startDate = new DateOnly(2025, 1, 1);

        var cmd = new CreateBudgetLineCommand(
            budgetId, groupId, null,
            "Rent", LineType.Expense,
            startDate, null,
            1500m, CurrencySeeds.GtqId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var line = await _db.BudgetLines.FindAsync(result.Value);
        line.ShouldNotBeNull();
        line.Name.ShouldBe("Rent");
        line.StartDate.ShouldBe(startDate);

        var revision = await _db.BudgetLineRevisions
            .FirstOrDefaultAsync(r => r.BudgetLineId == result.Value);
        revision.ShouldNotBeNull();
        revision.BudgetedAmount.ShouldBe(1500m);
        revision.CurrencyId.ShouldBe(CurrencySeeds.GtqId);
        revision.ValidFrom.ShouldBe(startDate);
        revision.ValidTo.ShouldBeNull();
    }

    [Fact]
    public async Task BudgetNotFound_Returns_Failure()
    {
        var cmd = new CreateBudgetLineCommand(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense,
            new DateOnly(2025, 1, 1), null,
            500m, CurrencySeeds.UsdId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("BUDGET_NOT_FOUND");
    }

    [Fact]
    public async Task ExplicitCurrencyId_UsedInRevision()
    {
        var (budgetId, groupId) = await SeedBudgetWithGroupAsync();

        var cmd = new CreateBudgetLineCommand(
            budgetId, groupId, null,
            "Rent", LineType.Expense,
            new DateOnly(2025, 1, 1), null,
            800m, CurrencySeeds.UsdId);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var revision = await _db.BudgetLineRevisions
            .FirstOrDefaultAsync(r => r.BudgetLineId == result.Value);
        revision.ShouldNotBeNull();
        revision.CurrencyId.ShouldBe(CurrencySeeds.UsdId);
    }
}
