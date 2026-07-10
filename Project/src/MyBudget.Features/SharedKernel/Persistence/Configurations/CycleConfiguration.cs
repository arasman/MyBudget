using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CycleConfiguration : IEntityTypeConfiguration<Cycle>
{
    public void Configure(EntityTypeBuilder<Cycle> builder)
    {
        builder.ToTable("Cycles");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasQueryFilter(c => c.DeletedAt == null);

        builder.HasIndex(c => c.BudgetId)
            .HasDatabaseName("IX_Cycles_BudgetId");

        // Partial unique index: only one active cycle per budget
        builder.HasIndex(c => new { c.BudgetId, c.IsActive })
            .HasDatabaseName("IX_Cycles_BudgetId_IsActive")
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        builder.HasOne(c => c.Budget)
            .WithMany()
            .HasForeignKey(c => c.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
