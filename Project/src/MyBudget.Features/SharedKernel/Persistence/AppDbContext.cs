using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence;

/// <summary>
/// Application EF Core DbContext. PURE — never inject IMediator here (ADR-006).
/// Global decimal precision (18,2) applied in OnModelCreating.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<User>                 Users                 => Set<User>();
    public DbSet<RefreshToken>         RefreshTokens         => Set<RefreshToken>();
    public DbSet<Budget>               Budgets               => Set<Budget>();
    public DbSet<BudgetMembership>     BudgetMemberships     => Set<BudgetMembership>();
    public DbSet<Invitation>           Invitations           => Set<Invitation>();
    public DbSet<Cycle>                Cycles                => Set<Cycle>();
    public DbSet<Period>               Periods               => Set<Period>();
    public DbSet<CategoryGroup>        CategoryGroups        => Set<CategoryGroup>();
    public DbSet<Category>             Categories            => Set<Category>();
    public DbSet<Currency>             Currencies            => Set<Currency>();
    public DbSet<BudgetLine>           BudgetLines           => Set<BudgetLine>();
    public DbSet<BudgetLineRevision>   BudgetLineRevisions   => Set<BudgetLineRevision>();

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
