using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        builder.ToTable("SecurityAuditLogs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Event)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .HasMaxLength(256);

        builder.Property(s => s.IpAddress)
            .HasMaxLength(45); // supports IPv6

        builder.Property(s => s.UserAgent)
            .HasMaxLength(512);

        builder.Property(s => s.Timestamp)
            .IsRequired();

        builder.Property(s => s.Details)
            .HasColumnType("text");

        // Index: user-scoped lookup
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_SecurityAuditLogs_UserId");

        // Index: event-type lookup
        builder.HasIndex(s => s.Event)
            .HasDatabaseName("IX_SecurityAuditLogs_Event");

        // Index: time-range queries (DESC)
        builder.HasIndex(s => s.Timestamp)
            .HasDatabaseName("IX_SecurityAuditLogs_Timestamp")
            .IsDescending(true);
    }
}
