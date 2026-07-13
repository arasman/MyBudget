using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(72);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        // UsedAt is nullable — NULL means the token has not been consumed yet
        builder.Property(t => t.UsedAt)
            .IsRequired(false);

        // Index by UserId — candidate tokens are found by user, then verified via BCrypt
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_PasswordResetTokens_UserId");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
