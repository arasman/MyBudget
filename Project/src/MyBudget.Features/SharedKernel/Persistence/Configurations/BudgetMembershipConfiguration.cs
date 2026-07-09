using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class BudgetMembershipConfiguration : IEntityTypeConfiguration<BudgetMembership>
{
    public void Configure(EntityTypeBuilder<BudgetMembership> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.BudgetId, m.UserId })
            .IsUnique()
            .HasDatabaseName("IX_BudgetMemberships_BudgetId_UserId");

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(m => m.Budget)
            .WithMany(b => b.Memberships)
            .HasForeignKey(m => m.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
