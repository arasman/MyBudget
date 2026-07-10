using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("BudgetLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.LineType)
            .HasConversion<int>();

        builder.HasQueryFilter(l => l.DeletedAt == null);

        builder.HasIndex(l => l.PeriodId)
            .HasDatabaseName("IX_BudgetLines_PeriodId");

        builder.HasIndex(l => l.CategoryGroupId)
            .HasDatabaseName("IX_BudgetLines_CategoryGroupId");

        builder.HasOne(l => l.Period)
            .WithMany(p => p.BudgetLines)
            .HasForeignKey(l => l.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.CategoryGroup)
            .WithMany()
            .HasForeignKey(l => l.CategoryGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Category)
            .WithMany()
            .HasForeignKey(l => l.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
