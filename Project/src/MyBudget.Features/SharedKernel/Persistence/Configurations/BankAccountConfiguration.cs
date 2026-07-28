using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Alias)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.BudgetId)
            .IsRequired();

        builder.Property(b => b.CurrencyId)
            .IsRequired();

        // Global soft-delete query filter
        builder.HasQueryFilter(b => b.DeletedAt == null);

        // FK: BudgetId -> Budgets (Restrict)
        builder.HasOne<Budget>()
            .WithMany()
            .HasForeignKey(b => b.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: CurrencyId -> Currencies (Restrict)
        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(b => b.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BudgetId)
            .HasDatabaseName("IX_BankAccounts_BudgetId");
    }
}
