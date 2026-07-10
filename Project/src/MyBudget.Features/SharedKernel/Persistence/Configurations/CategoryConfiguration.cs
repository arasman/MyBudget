using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasQueryFilter(c => c.DeletedAt == null);

        builder.HasIndex(c => c.CategoryGroupId)
            .HasDatabaseName("IX_Categories_CategoryGroupId");

        builder.HasIndex(c => new { c.CategoryGroupId, c.Name })
            .HasDatabaseName("IX_Categories_CategoryGroupId_Name")
            .IsUnique();

        builder.HasOne(c => c.CategoryGroup)
            .WithMany(g => g.Categories)
            .HasForeignKey(c => c.CategoryGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
