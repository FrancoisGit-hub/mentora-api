using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CreditBalanceConfiguration : IEntityTypeConfiguration<CreditBalance>
{
    public void Configure(EntityTypeBuilder<CreditBalance> builder)
    {
        builder.ToTable("CREDIT_BALANCES");

        builder.HasKey(e => e.CreditBalanceId);
        builder.Property(e => e.CreditBalanceId)
            .HasColumnName("CREDIT_BALANCE_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CreditBalanceAmount).HasColumnName("CREDIT_BALANCE_AMOUNT").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CreditBalanceUpdatedDate).HasColumnName("CREDIT_BALANCE_UPDATED_DATE").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();

        builder.HasIndex(e => new { e.UserId, e.CoachId }).IsUnique();

        builder.HasOne(e => e.User)
            .WithMany(u => u.CreditBalances)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.CreditBalances)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
