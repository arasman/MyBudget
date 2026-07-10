using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class BudgetLineRevisionConfiguration : IEntityTypeConfiguration<BudgetLineRevision>
{
    public void Configure(EntityTypeBuilder<BudgetLineRevision> builder)
    {
        builder.ToTable("BudgetLineRevisions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(r => r.BudgetedAmount)
            .HasPrecision(18, 2);

        // No query filter — BudgetLineRevision is immutable/append-only; no soft delete

        builder.HasIndex(r => new { r.BudgetLineId, r.RevisedAt })
            .HasDatabaseName("IX_BudgetLineRevisions_BudgetLineId_RevisedAt")
            .IsDescending(false, true);

        builder.HasOne(r => r.BudgetLine)
            .WithMany(l => l.Revisions)
            .HasForeignKey(r => r.BudgetLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
