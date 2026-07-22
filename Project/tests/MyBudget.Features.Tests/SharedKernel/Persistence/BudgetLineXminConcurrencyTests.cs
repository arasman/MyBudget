using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Persistence;

/// <summary>
/// Verifies that BudgetLineConfiguration's xmin concurrency token does NOT cause an exception
/// when the AppDbContext is used with SQLite (unit / CI environment).
/// REQ-BL-CONCURRENCY-1: guard must be provider-conditional.
/// </summary>
public sealed class BudgetLineXminConcurrencyTests : IDisposable
{
    private readonly AppDbContext _db;

    public BudgetLineXminConcurrencyTests()
    {
        _db = DbTestHelpers.CreateSqliteContext();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void EnsureCreated_WithSqlite_DoesNotThrow()
    {
        // AppDbContext.EnsureCreated() is called in DbTestHelpers.CreateSqliteContext()
        // (via Database.EnsureCreated()). If xmin were configured unconditionally, EF would
        // attempt to map the PostgreSQL system column on SQLite and throw at model-build time.
        // Getting here without an exception means the provider guard is working.
        _db.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadBudgetLine_WithSqlite_DoesNotThrow()
    {
        // Seed minimum data and load a BudgetLine through EF — verifies that
        // the concurrency token configuration is inert on SQLite.
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var group = CategoryGroup.Create(budgetId, "Group A", 0);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var line = BudgetLine.Create(
            budgetId, group.Id, null,
            "Rent", LineType.Expense,
            new DateOnly(2025, 1, 1), null,
            1000m, CurrencySeeds.GtqId);
        _db.BudgetLines.Add(line);

        await Should.NotThrowAsync(() => _db.SaveChangesAsync());

        var loaded = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == line.Id);
        loaded.ShouldNotBeNull();
    }
}
