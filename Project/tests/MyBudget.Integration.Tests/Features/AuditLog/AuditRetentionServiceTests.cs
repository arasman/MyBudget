using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Services;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.AuditLog;

/// <summary>
/// Integration tests for <see cref="AuditRetentionService"/>.
/// PR5 tasks 5.4 and 5.5.
/// </summary>
public sealed class AuditRetentionServiceTests : IntegrationTestBase
{
    public AuditRetentionServiceTests(IntegrationTestFactory factory) : base(factory) { }

    // -------------------------------------------------------------------------
    // 5.4 — AuditLog rows older than TTL are deleted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteCleanupAsync_DeletesAuditLogRows_OlderThanTtl()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Insert one AuditLog row with Timestamp 91 days in the past (older than 90-day TTL)
        var oldId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AuditLogs" ("Id", "EntityName", "EntityId", "Action", "UserId", "Timestamp", "BeforeJson", "AfterJson", "BudgetId")
            VALUES ({0}, 'Budget', {1}, 'Created', NULL, {2}, NULL, NULL, NULL)
            """,
            oldId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-91));

        var svc = scope.ServiceProvider.GetServices<IHostedService>()
            .OfType<AuditRetentionService>()
            .Single();
        await svc.ExecuteCleanupAsync();

        var exists = await db.AuditLogs.AnyAsync(a => a.Id == oldId);
        exists.ShouldBeFalse("Row older than TTL should have been deleted");
    }

    // -------------------------------------------------------------------------
    // 5.5 — AuditLog rows within TTL window are preserved
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteCleanupAsync_PreservesAuditLogRows_WithinTtlWindow()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Insert one AuditLog row with Timestamp 1 day in the past (within 90-day TTL)
        var recentId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AuditLogs" ("Id", "EntityName", "EntityId", "Action", "UserId", "Timestamp", "BeforeJson", "AfterJson", "BudgetId")
            VALUES ({0}, 'Budget', {1}, 'Created', NULL, {2}, NULL, NULL, NULL)
            """,
            recentId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));

        var svc = scope.ServiceProvider.GetServices<IHostedService>()
            .OfType<AuditRetentionService>()
            .Single();
        await svc.ExecuteCleanupAsync();

        var exists = await db.AuditLogs.AnyAsync(a => a.Id == recentId);
        exists.ShouldBeTrue("Row within TTL window should NOT have been deleted");
    }
}
