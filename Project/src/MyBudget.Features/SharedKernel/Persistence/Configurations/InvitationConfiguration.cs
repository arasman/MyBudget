using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InviteeEmail)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(i => i.TokenHash)
            .IsRequired()
            .HasMaxLength(72);

        builder.Property(i => i.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(i => i.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Invitations_TokenHash");

        builder.HasIndex(i => i.InviteeEmail)
            .HasDatabaseName("IX_Invitations_InviteeEmail");

        builder.HasOne(i => i.Budget)
            .WithMany(b => b.Invitations)
            .HasForeignKey(i => i.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InvitedByUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
