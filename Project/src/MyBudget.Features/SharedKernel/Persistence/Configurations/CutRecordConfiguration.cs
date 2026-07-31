using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CutRecordConfiguration : IEntityTypeConfiguration<CutRecord>
{
    public void Configure(EntityTypeBuilder<CutRecord> builder)
    {
        builder.ToTable("CutRecords");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.BudgetId)
            .IsRequired();

        builder.Property(c => c.ExchangeRate)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(c => c.ProjectionsJson)
            .HasColumnType("text");

        // CS-6: 16 persisted totals — decimal(18,2), matching CutBankAccount.BalanceInPrimary.
        builder.Property(c => c.TotalPositive).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalPositiveAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalNegative).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalNegativeAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalDeudaEnCurso).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalDeudaEnCursoAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalBudgeted).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalBudgetedAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalRegistered).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalRegisteredAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.Remaining).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.RemainingAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalAvailable).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalAvailableAlt).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalNet).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalNetAlt).HasPrecision(18, 2).IsRequired();

        // FK: BudgetId -> Budgets (Restrict)
        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(c => c.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE(BudgetId, CutDate)
        builder.HasIndex(c => new { c.BudgetId, c.CutDate })
            .IsUnique()
            .HasDatabaseName("UQ_CutRecords_BudgetId_CutDate");

        builder.HasMany(c => c.CutBankAccounts)
            .WithOne(cba => cba.CutRecord)
            .HasForeignKey(cba => cba.CutRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
