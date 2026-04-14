using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("CREDIT_TRANSACTIONS");

        builder.HasKey(e => e.CreditTransactionId);
        builder.Property(e => e.CreditTransactionId)
            .HasColumnName("CREDIT_TRANSACTION_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CreditTransactionType).HasColumnName("CREDIT_TRANSACTION_TYPE").IsRequired().HasMaxLength(20);
        builder.Property(e => e.CreditTransactionAmount).HasColumnName("CREDIT_TRANSACTION_AMOUNT").IsRequired();
        builder.Property(e => e.CreditTransactionEuros).HasColumnName("CREDIT_TRANSACTION_EUROS").HasColumnType("decimal(8,2)");
        builder.Property(e => e.CreditTransactionDate).HasColumnName("CREDIT_TRANSACTION_DATE").IsRequired();
        builder.Property(e => e.SessionId).HasColumnName("SESSION_ID");
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.User)
            .WithMany(u => u.CreditTransactions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Coach)
            .WithMany(c => c.CreditTransactions)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
