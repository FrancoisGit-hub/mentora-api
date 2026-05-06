using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CART_ITEMS", t =>
            t.HasCheckConstraint(
                "CK_CART_ITEMS_PRODUCT_OR_PACK_XOR",
                "((\"CART_ITEM_PRODUCT_ID\" IS NOT NULL)::int + (\"CART_ITEM_PACK_ID\" IS NOT NULL)::int) = 1"));

        builder.HasKey(e => e.CartItemId);
        builder.Property(e => e.CartItemId)
            .HasColumnName("CART_ITEM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.CartId)
            .HasColumnName("CART_ITEM_CART_ID")
            .IsRequired();

        builder.Property(e => e.ProductId)
            .HasColumnName("CART_ITEM_PRODUCT_ID");

        builder.Property(e => e.PackId)
            .HasColumnName("CART_ITEM_PACK_ID");

        builder.Property(e => e.Quantity)
            .HasColumnName("CART_ITEM_QUANTITY")
            .IsRequired();

        builder.Property(e => e.AddedDate)
            .HasColumnName("CART_ITEM_ADDED_DATE")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(e => e.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(e => e.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Pack)
            .WithMany()
            .HasForeignKey(e => e.PackId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
