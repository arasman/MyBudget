using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.Tests.Helpers;

/// <summary>
/// Creates an in-memory SQLite DbContext seeded with the minimum required FK chain:
/// User -> Budget. Returns both the context and the BudgetId for use in handler tests.
/// </summary>
public static class DbTestHelpers
{
    public static AppDbContext CreateSqliteContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new AppDbContext(opts);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Seeds a User and Budget and returns the BudgetId.</summary>
    public static async Task<Guid> SeedBudgetAsync(AppDbContext db)
    {
        var user = User.Create("test@example.com", "hash", "Test", "User");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var budget = Budget.Create("Test Budget", user.Id);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync();

        return budget.Id;
    }
}
