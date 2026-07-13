using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.PasswordHash)
            .IsRequired()
            .HasMaxLength(72);

        builder.Property(h => h.CreatedAt)
            .IsRequired();

        builder.HasIndex(h => new { h.UserId, h.CreatedAt })
            .HasDatabaseName("IX_PasswordHistory_UserId_CreatedAt");

        builder.HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
