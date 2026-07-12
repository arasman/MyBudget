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

        builder.Property(r => r.BudgetId)
            .IsRequired();

        builder.Property(r => r.CurrencyId)
            .IsRequired();

        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(r => r.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Currency)
            .WithMany()
            .HasForeignKey(r => r.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.BudgetedAmount)
            .HasPrecision(18, 2);

        // No query filter — BudgetLineRevision is immutable/append-only; no soft delete

        builder.HasIndex(r => r.BudgetId)
            .HasDatabaseName("IX_BudgetLineRevisions_BudgetId");

        builder.HasIndex(r => new { r.BudgetLineId, r.RevisedAt })
            .HasDatabaseName("IX_BudgetLineRevisions_BudgetLineId_RevisedAt")
            .IsDescending(false, true);

        builder.HasOne(r => r.BudgetLine)
            .WithMany(l => l.Revisions)
            .HasForeignKey(r => r.BudgetLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
