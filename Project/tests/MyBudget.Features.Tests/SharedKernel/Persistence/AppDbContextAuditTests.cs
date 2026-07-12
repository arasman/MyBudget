using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Services;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Persistence;

/// <summary>
/// Unit tests for the SaveChangesAsync override in AppDbContext.
/// Verifies AuditLog entry creation for Created/Updated/Deleted/Restored actions,
/// non-whitelisted entity exclusion, and unauthenticated UserId behaviour.
/// </summary>
public sealed class AppDbContextAuditTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _userService;
    private readonly Guid _userId = Guid.NewGuid();

    public AppDbContextAuditTests()
    {
        _userService = Substitute.For<ICurrentUserService>();
        _userService.UserId.Returns(_userId);

        _db = CreateContext(_userService);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------------------
    // Helper factories
    // ---------------------------------------------------------------------------

    private static AppDbContext CreateContext(ICurrentUserService? userService = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        return new AppDbContext(opts, userService);
    }

    /// <summary>Seeds the minimum User+Budget FK chain without triggering audit rows on them.</summary>
    private async Task<(Guid userId, Guid budgetId)> SeedMinimalAsync()
    {
        // User and Budget are IAuditableEntity but we need existing rows
        // for FK constraints. The audit rows produced here are expected side-effects.
        var user = User.Create("audit@test.com", "hash", "Audit", "Test");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var budget = Budget.Create("AuditBudget", user.Id);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        return (user.Id, budget.Id);
    }

    // ---------------------------------------------------------------------------
    // 2.4 — Added entity → Action = Created, BeforeJson = null, AfterJson populated
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Added_WhitelistedEntity_Produces_Created_AuditLog()
    {
        var (_, budgetId) = await SeedMinimalAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        var entry = await _db.AuditLogs
            .Where(a => a.EntityName == "CategoryGroup" && a.EntityId == group.Id)
            .FirstOrDefaultAsync();

        entry.ShouldNotBeNull();
        entry.Action.ShouldBe("Created");
        entry.BeforeJson.ShouldBeNull();
        entry.AfterJson.ShouldNotBeNull();
        entry.UserId.ShouldBe(_userId);
    }

    // ---------------------------------------------------------------------------
    // 2.5 — Modified entity (no DeletedAt change) → Action = Updated, both snapshots
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Modified_NoDeletedAtChange_Produces_Updated_AuditLog()
    {
        var (_, budgetId) = await SeedMinimalAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        // Modify — rename
        group.Update("Housing Renamed", 1);
        await _db.SaveChangesAsync();

        var entry = await _db.AuditLogs
            .Where(a => a.EntityName == "CategoryGroup" && a.EntityId == group.Id && a.Action == "Updated")
            .FirstOrDefaultAsync();

        entry.ShouldNotBeNull();
        entry.BeforeJson.ShouldNotBeNull();
        entry.AfterJson.ShouldNotBeNull();
    }

    // ---------------------------------------------------------------------------
    // 2.6 — Modified entity DeletedAt null→value → Action = Deleted, AfterJson = null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Modified_DeletedAt_NullToValue_Produces_Deleted_AuditLog()
    {
        var (_, budgetId) = await SeedMinimalAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        group.SoftDelete();
        await _db.SaveChangesAsync();

        var entry = await _db.AuditLogs
            .Where(a => a.EntityName == "CategoryGroup" && a.EntityId == group.Id && a.Action == "Deleted")
            .FirstOrDefaultAsync();

        entry.ShouldNotBeNull();
        entry.BeforeJson.ShouldNotBeNull();
        entry.AfterJson.ShouldBeNull();
    }

    // ---------------------------------------------------------------------------
    // 2.7 — Modified entity DeletedAt value→null → Action = Restored
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Modified_DeletedAt_ValueToNull_Produces_Restored_AuditLog()
    {
        var (_, budgetId) = await SeedMinimalAsync();

        var group = CategoryGroup.Create(budgetId, "Housing", 1);
        group.SoftDelete();
        _db.CategoryGroups.Add(group);
        await _db.SaveChangesAsync();

        group.Restore();
        await _db.SaveChangesAsync();

        var entry = await _db.AuditLogs
            .Where(a => a.EntityName == "CategoryGroup" && a.EntityId == group.Id && a.Action == "Restored")
            .FirstOrDefaultAsync();

        entry.ShouldNotBeNull();
        entry.BeforeJson.ShouldNotBeNull();
        entry.AfterJson.ShouldNotBeNull();
    }

    // ---------------------------------------------------------------------------
    // 2.8 — Non-whitelisted entity save → zero AuditLog rows for that entity
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NonWhitelisted_Entity_Produces_No_AuditLog()
    {
        // User does NOT implement IAuditableEntity — it is a non-whitelisted entity
        // (User extends BaseEntity but does not implement IAuditableEntity)
        var before = await _db.AuditLogs.CountAsync();

        var user = User.Create("noaudit@test.com", "hash", "No", "Audit");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var after = await _db.AuditLogs.CountAsync();

        // No new AuditLog rows should exist for the User entity
        after.ShouldBe(before);
        var userEntry = await _db.AuditLogs
            .Where(a => a.EntityName == "User")
            .FirstOrDefaultAsync();
        userEntry.ShouldBeNull();
    }

    // ---------------------------------------------------------------------------
    // 2.9 — No authenticated user → AuditLog.UserId = null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Unauthenticated_Context_Produces_AuditLog_With_Null_UserId()
    {
        // Create a separate context with no ICurrentUserService
        using var anonDb = CreateContext(userService: null);
        anonDb.Database.OpenConnection();
        anonDb.Database.EnsureCreated();

        // Seed the FK chain manually in the anonymous context
        var user = User.Create("anon@test.com", "hash", "Anon", "User");
        anonDb.Users.Add(user);
        await anonDb.SaveChangesAsync();

        var budget = Budget.Create("AnonBudget", user.Id);
        anonDb.Budgets.Add(budget);
        await anonDb.SaveChangesAsync();

        var group = CategoryGroup.Create(budget.Id, "Groceries", 1);
        anonDb.CategoryGroups.Add(group);
        await anonDb.SaveChangesAsync();

        var entry = await anonDb.AuditLogs
            .Where(a => a.EntityName == "CategoryGroup" && a.EntityId == group.Id)
            .FirstOrDefaultAsync();

        entry.ShouldNotBeNull();
        entry.Action.ShouldBe("Created");
        entry.UserId.ShouldBeNull();
    }
}
