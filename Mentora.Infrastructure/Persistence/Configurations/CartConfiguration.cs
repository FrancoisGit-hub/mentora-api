using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("CARTS");

        builder.HasKey(e => e.CartId);
        builder.Property(e => e.CartId)
            .HasColumnName("CART_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberId)
            .HasColumnName("CART_MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("CART_COACH_ID")
            .IsRequired();

        builder.Property(e => e.CreatedDate)
            .HasColumnName("CART_CREATED_DATE")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.UpdatedDate)
            .HasColumnName("CART_UPDATED_DATE")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // One cart per member/coach pair
        builder.HasIndex(e => new { e.MemberId, e.CoachId })
            .IsUnique()
            .HasDatabaseName("IX_CARTS_MEMBER_COACH_UNIQUE");

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
