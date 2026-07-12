using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.BeforeJson)
            .HasColumnType("text");

        builder.Property(a => a.AfterJson)
            .HasColumnType("text");

        // Index: budget-scoped time-range queries
        builder.HasIndex(a => new { a.BudgetId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_BudgetId_Timestamp")
            .IsDescending(false, true);

        // Index: entity-scoped lookup
        builder.HasIndex(a => new { a.EntityName, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_EntityName_EntityId");

        // Index: user-scoped lookup
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogs_UserId");
    }
}
