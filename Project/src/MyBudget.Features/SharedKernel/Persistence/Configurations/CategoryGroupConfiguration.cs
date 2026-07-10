using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CategoryGroupConfiguration : IEntityTypeConfiguration<CategoryGroup>
{
    public void Configure(EntityTypeBuilder<CategoryGroup> builder)
    {
        builder.ToTable("CategoryGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasQueryFilter(g => g.DeletedAt == null);

        builder.HasIndex(g => g.BudgetId)
            .HasDatabaseName("IX_CategoryGroups_BudgetId");

        builder.HasIndex(g => new { g.BudgetId, g.Name })
            .HasDatabaseName("IX_CategoryGroups_BudgetId_Name")
            .IsUnique();

        builder.HasOne(g => g.Budget)
            .WithMany()
            .HasForeignKey(g => g.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
