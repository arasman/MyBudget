using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.DeletedAt)
            .IsRequired(false);

        builder.HasOne(b => b.Owner)
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
