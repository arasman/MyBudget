using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Currencies_Code");

        builder.HasData(
            new { Id = CurrencySeeds.GtqId, Code = "GTQ", Name = "Quetzal",   Symbol = "Q" },
            new { Id = CurrencySeeds.UsdId, Code = "USD", Name = "US Dollar", Symbol = "$" },
            new { Id = CurrencySeeds.EurId, Code = "EUR", Name = "Euro",      Symbol = "€" }
        );
    }
}
