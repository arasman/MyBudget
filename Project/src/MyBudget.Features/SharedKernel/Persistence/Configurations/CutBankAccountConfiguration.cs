using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CutBankAccountConfiguration : IEntityTypeConfiguration<CutBankAccount>
{
    public void Configure(EntityTypeBuilder<CutBankAccount> builder)
    {
        builder.ToTable("CutBankAccounts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Alias)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CutRecordId)
            .IsRequired();

        builder.Property(c => c.BankAccountId)
            .IsRequired();

        builder.Property(c => c.CurrencyId)
            .IsRequired();

        builder.Property(c => c.Balance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.BalanceInPrimary)
            .HasPrecision(18, 2)
            .IsRequired();

        // FK: CutRecordId -> CutRecords (Cascade — configured on CutRecord side)
        builder.HasOne(c => c.CutRecord)
            .WithMany(cr => cr.CutBankAccounts)
            .HasForeignKey(c => c.CutRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: BankAccountId -> BankAccounts (Restrict — soft-delete always allowed)
        builder.HasOne(c => c.BankAccount)
            .WithMany()
            .HasForeignKey(c => c.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE(CutRecordId, BankAccountId)
        builder.HasIndex(c => new { c.CutRecordId, c.BankAccountId })
            .IsUnique()
            .HasDatabaseName("UQ_CutBankAccounts_CutRecordId_BankAccountId");

        builder.HasIndex(c => c.CutRecordId)
            .HasDatabaseName("IX_CutBankAccounts_CutRecordId");
    }
}
