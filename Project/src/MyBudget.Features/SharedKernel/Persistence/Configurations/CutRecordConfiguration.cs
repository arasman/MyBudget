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
