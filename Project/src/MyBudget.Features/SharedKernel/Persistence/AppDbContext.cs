using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Services;

namespace MyBudget.Features.SharedKernel.Persistence;

/// <summary>
/// Application EF Core DbContext. PURE — never inject IMediator here (ADR-006).
/// Global decimal precision (18,2) applied in OnModelCreating.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> opts, ICurrentUserService? currentUserService = null)
        : base(opts)
    {
        _currentUserService = currentUserService;
    }

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
    public DbSet<AuditLog>             AuditLogs             => Set<AuditLog>();
    public DbSet<SecurityAuditLog>     SecurityAuditLogs     => Set<SecurityAuditLog>();

    // ---------------------------------------------------------------------------
    // SaveChangesAsync override — audit log interception (PR2)
    // ---------------------------------------------------------------------------

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        var result = await base.SaveChangesAsync(cancellationToken);

        if (auditEntries.Count > 0)
        {
            // EF assigns the Guid PK on first save for Added entries; now EntityId is populated.
            AuditLogs.AddRange(auditEntries);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private List<AuditLog> BuildAuditEntries()
    {
        var userId  = _currentUserService?.UserId;
        var entries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.Entity is not IAuditableEntity auditable)
                continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var action     = DetectAction(entry);
            var entityName = entry.Entity.GetType().Name;
            var entityId   = entry.Entity.Id;
            var budgetId   = auditable.ResolveBudgetId();
            var beforeJson = BuildBeforeJson(entry);
            var afterJson  = BuildAfterJson(entry);

            entries.Add(AuditLog.Create(entityName, entityId, action, userId, beforeJson, afterJson, budgetId));
        }

        return entries;
    }

    private static string DetectAction(EntityEntry<BaseEntity> entry)
    {
        if (entry.State == EntityState.Added)
            return "Created";

        if (entry.State == EntityState.Deleted)
            return "Deleted";

        // EntityState.Modified — inspect DeletedAt transitions for soft-delete semantics
        var deletedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");

        if (deletedAtProp is not null)
        {
            var original = deletedAtProp.OriginalValue;
            var current  = deletedAtProp.CurrentValue;

            if (original is null && current is not null)
                return "Deleted";

            if (original is not null && current is null)
                return "Restored";
        }

        return "Updated";
    }

    private static string? BuildBeforeJson(EntityEntry<BaseEntity> entry)
    {
        if (entry.State == EntityState.Added)
            return null;

        var dict = entry.OriginalValues.Properties
            .ToDictionary(p => p.Name, p => entry.OriginalValues[p]);

        return JsonSerializer.Serialize(dict);
    }

    private static string? BuildAfterJson(EntityEntry<BaseEntity> entry)
    {
        if (entry.State == EntityState.Deleted)
            return null;

        // For soft-delete (Modified + DeletedAt null→value) also return null
        if (entry.State == EntityState.Modified)
        {
            var deletedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
            if (deletedAtProp is not null
                && deletedAtProp.OriginalValue is null
                && deletedAtProp.CurrentValue is not null)
            {
                return null;
            }
        }

        var dict = entry.CurrentValues.Properties
            .ToDictionary(p => p.Name, p => entry.CurrentValues[p]);

        return JsonSerializer.Serialize(dict);
    }

    // ---------------------------------------------------------------------------

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
