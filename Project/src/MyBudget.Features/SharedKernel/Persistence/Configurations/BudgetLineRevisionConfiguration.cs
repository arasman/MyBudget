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

        builder.Property(r => r.ValidFrom)
            .IsRequired()
            .HasColumnType("TEXT"); // DateOnly stored as ISO text in SQLite

        builder.Property(r => r.ValidTo)
            .HasColumnType("TEXT"); // DateOnly? — nullable

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

        // REQ-BL-NOTE-MAX-1: Note max 200 chars.
        builder.Property(r => r.Note)
            .HasMaxLength(200);

        // No query filter — BudgetLineRevision is append-only; no soft delete

        builder.HasIndex(r => r.BudgetId)
            .HasDatabaseName("IX_BudgetLineRevisions_BudgetId");

        builder.HasIndex(r => new { r.BudgetLineId, r.ValidFrom })
            .HasDatabaseName("IX_BudgetLineRevisions_BudgetLineId_ValidFrom");

        builder.HasOne(r => r.BudgetLine)
            .WithMany(l => l.Revisions)
            .HasForeignKey(r => r.BudgetLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
