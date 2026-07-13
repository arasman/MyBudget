using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class ExecutionRecordConfiguration : IEntityTypeConfiguration<ExecutionRecord>
{
    public void Configure(EntityTypeBuilder<ExecutionRecord> builder)
    {
        builder.ToTable("ExecutionRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Note)
            .HasMaxLength(500);

        builder.Property(e => e.ExchangeRate)
            .HasPrecision(18, 6);

        builder.Property(e => e.ExchangeRateTo)
            .HasPrecision(18, 6);

        builder.Property(e => e.BudgetId)
            .IsRequired();

        builder.Property(e => e.PeriodId)
            .IsRequired();

        builder.Property(e => e.BudgetLineId)
            .IsRequired();

        builder.Property(e => e.CurrencyId)
            .IsRequired();

        // AccountId and PaymentMethodId: nullable, NO FK constraint
        builder.Property(e => e.AccountId);
        builder.Property(e => e.PaymentMethodId);

        // Global soft-delete query filter
        builder.HasQueryFilter(e => e.DeletedAt == null);

        // FK: BudgetLineId -> BudgetLines (Restrict — execution records must be cleaned up before deleting lines)
        builder.HasOne(e => e.BudgetLine)
            .WithMany()
            .HasForeignKey(e => e.BudgetLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: CurrencyId -> Currencies (Restrict)
        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: PeriodId -> Periods (Restrict — denormalized for fast RBAC)
        builder.HasOne<Period>()
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite indexes
        builder.HasIndex(e => new { e.BudgetLineId, e.DeletedAt })
            .HasDatabaseName("IX_ExecutionRecords_BudgetLineId_DeletedAt");

        builder.HasIndex(e => new { e.BudgetLineId, e.DeletedAt, e.EntryType })
            .HasDatabaseName("IX_ExecutionRecords_BudgetLineId_DeletedAt_EntryType");

        builder.HasIndex(e => new { e.PeriodId, e.DeletedAt })
            .HasDatabaseName("IX_ExecutionRecords_PeriodId_DeletedAt");

        builder.HasIndex(e => e.BudgetId)
            .HasDatabaseName("IX_ExecutionRecords_BudgetId");
    }
}
