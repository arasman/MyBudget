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
/// JWT__Key is set via environment variable or appsettings defaults.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=mybudget_test;Username=mybudget;Password=mybudget;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Use a dedicated test DB so integration tests don't contaminate dev data
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
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

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration and replace with test DB
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(TestConnectionString));
        });
    }

    // IAsyncLifetime — called once by xUnit before any test in the collection runs
    public async Task InitializeAsync() => await InitializeDatabaseAsync();
    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task CleanDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Clear in FK-dependency reverse order (budget structure first, then auth)
        db.BudgetLineRevisions.RemoveRange(db.BudgetLineRevisions.IgnoreQueryFilters());
        db.BudgetLines.RemoveRange(db.BudgetLines.IgnoreQueryFilters());
        db.Periods.RemoveRange(db.Periods.IgnoreQueryFilters());
        db.Cycles.RemoveRange(db.Cycles.IgnoreQueryFilters());
        db.Categories.RemoveRange(db.Categories.IgnoreQueryFilters());
        db.CategoryGroups.RemoveRange(db.CategoryGroups.IgnoreQueryFilters());
        db.Invitations.RemoveRange(db.Invitations);
        db.BudgetMemberships.RemoveRange(db.BudgetMemberships);
        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        db.Budgets.RemoveRange(db.Budgets);
        db.Users.RemoveRange(db.Users);
        // Audit tables — no FK constraints, cleared independently
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.SecurityAuditLogs.RemoveRange(db.SecurityAuditLogs);
        await db.SaveChangesAsync();
    }
}
