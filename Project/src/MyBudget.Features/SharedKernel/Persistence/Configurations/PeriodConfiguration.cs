using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class PeriodConfiguration : IEntityTypeConfiguration<Period>
{
    public void Configure(EntityTypeBuilder<Period> builder)
    {
        builder.ToTable("Periods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.BudgetId)
            .IsRequired();

        builder.HasQueryFilter(p => p.DeletedAt == null);

        builder.HasIndex(p => p.BudgetId)
            .HasDatabaseName("IX_Periods_BudgetId");

        builder.HasIndex(p => p.CycleId)
            .HasDatabaseName("IX_Periods_CycleId");

        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(p => p.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Cycle)
            .WithMany(c => c.Periods)
            .HasForeignKey(p => p.CycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
