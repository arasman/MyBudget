using Microsoft.EntityFrameworkCore;

namespace MyBudget.Features.SharedKernel.Persistence;

/// <summary>
/// Application EF Core DbContext. PURE — never inject IMediator here (ADR-006).
/// Global decimal precision (18,2) applied in OnModelCreating.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Global decimal precision — critical for monetary values
        foreach (var property in builder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
