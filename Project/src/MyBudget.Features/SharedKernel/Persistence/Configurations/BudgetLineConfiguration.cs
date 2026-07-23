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

        builder.Property(l => l.Description)
            .HasColumnName("Description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(l => l.LineType)
            .HasConversion<int>();

        builder.Property(l => l.DisplayOrder)
            .IsRequired();

        builder.Property(l => l.BudgetId)
            .IsRequired();

        builder.Property(l => l.StartDate)
            .IsRequired()
            .HasColumnType("TEXT"); // DateOnly stored as ISO text in SQLite

        builder.Property(l => l.EndDate)
            .HasColumnType("TEXT"); // DateOnly? — nullable

        builder.HasQueryFilter(l => l.DeletedAt == null);

        builder.HasIndex(l => l.BudgetId)
            .HasDatabaseName("IX_BudgetLines_BudgetId");

        builder.HasIndex(l => l.CategoryGroupId)
            .HasDatabaseName("IX_BudgetLines_CategoryGroupId");

        // REQ-BL-NAME-1: unique name per Budget (includes soft-deleted — no filter clause)
        builder.HasIndex(l => new { l.BudgetId, l.Name })
            .IsUnique()
            .HasDatabaseName("IX_BudgetLines_BudgetId_Name");

        builder.HasIndex(l => new { l.BudgetId, l.StartDate })
            .HasDatabaseName("IX_BudgetLines_BudgetId_StartDate");

        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(l => l.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

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
