using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Integration.Tests.Infrastructure;

/// <summary>
/// Integration test factory that overrides configuration to point at the test Postgres DB.
/// Requires Docker Compose stack running (Postgres on port 5432).
/// Connection string is read from appsettings.Testing.json — no hardcoded constants.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _testConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // appsettings.Testing.json is auto-loaded by ASP.NET Core because UseEnvironment("Testing")
            // is set above — no explicit AddJsonFile needed. The connection string in
            // appsettings.Testing.json is the single source of truth; no hardcoded constants.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // JWT key for integration tests — not a secret
                ["JWT__Key"] = "IntegrationTest-Secret-Key-MinLength32!!",
                ["JWT:Key"]  = "IntegrationTest-Secret-Key-MinLength32!!",
                ["JWT:Issuer"]   = "MyBudget",
                ["JWT:Audience"] = "MyBudget.Client",
                ["JWT:AccessTokenExpiryMinutes"] = "15",
                // Disable SMTP for tests
                ["Email:Host"] = "localhost",
                ["Email:Port"] = "1025",
            });
        });

        builder.ConfigureServices((ctx, services) =>
        {
            _testConnectionString = ctx.Configuration["ConnectionStrings:DefaultConnection"]
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is missing from appsettings.Testing.json");

            // Remove existing DbContext registration and replace with test DB
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_testConnectionString));
        });
    }

    // IAsyncLifetime — called once by xUnit before any test in the collection runs
    public async Task InitializeAsync() => await InitializeDatabaseAsync();
    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Drop and recreate to avoid "relation already exists" errors when schema changes
        // across feature branches. Safe because the test DB is isolated and ephemeral.
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task CleanDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Clear in FK-dependency reverse order (budget structure first, then auth)
        db.CutBankAccounts.RemoveRange(db.CutBankAccounts);
        db.CutRecords.RemoveRange(db.CutRecords);
        db.BankAccounts.RemoveRange(db.BankAccounts.IgnoreQueryFilters());
        db.ExecutionRecords.RemoveRange(db.ExecutionRecords.IgnoreQueryFilters());
        db.BudgetLineRevisions.RemoveRange(db.BudgetLineRevisions.IgnoreQueryFilters());
        db.BudgetLines.RemoveRange(db.BudgetLines.IgnoreQueryFilters());
        db.Periods.RemoveRange(db.Periods.IgnoreQueryFilters());
        db.Cycles.RemoveRange(db.Cycles.IgnoreQueryFilters());
        db.Categories.RemoveRange(db.Categories.IgnoreQueryFilters());
        db.CategoryGroups.RemoveRange(db.CategoryGroups.IgnoreQueryFilters());
        db.Invitations.RemoveRange(db.Invitations);
        db.BudgetMemberships.RemoveRange(db.BudgetMemberships);
        db.PasswordResetTokens.RemoveRange(db.PasswordResetTokens);
        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        db.Budgets.RemoveRange(db.Budgets);
        db.Users.RemoveRange(db.Users);
        // Audit tables — no FK constraints, cleared independently
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.SecurityAuditLogs.RemoveRange(db.SecurityAuditLogs);
        await db.SaveChangesAsync();
    }
}
