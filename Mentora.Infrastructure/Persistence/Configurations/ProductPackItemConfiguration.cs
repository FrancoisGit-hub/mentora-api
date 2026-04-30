using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProductPackItemConfiguration : IEntityTypeConfiguration<ProductPackItem>
{
    public void Configure(EntityTypeBuilder<ProductPackItem> builder)
    {
        builder.ToTable("PRODUCT_PACK_ITEMS", t =>
            t.HasCheckConstraint(
                "CK_PRODUCT_PACK_ITEMS_QUANTITY_MIN_1",
                "\"PRODUCT_PACK_ITEM_QUANTITY\" >= 1"));

        builder.HasKey(e => e.ProductPackItemId);
        builder.Property(e => e.ProductPackItemId)
            .HasColumnName("PRODUCT_PACK_ITEM_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProductPackItemQuantity)
            .HasColumnName("PRODUCT_PACK_ITEM_QUANTITY")
            .IsRequired();

        builder.Property(e => e.ProductPackId)
            .HasColumnName("PRODUCT_PACK_ID")
            .IsRequired();

        builder.Property(e => e.ProductId)
            .HasColumnName("PRODUCT_ID")
            .IsRequired();

        // Unique: a product can appear only once per pack; quantity carries the multiplicity
        builder.HasIndex(e => new { e.ProductPackId, e.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_PRODUCT_PACK_ITEMS_PACK_PRODUCT_UNIQUE");

        builder.HasOne(e => e.ProductPack)
            .WithMany(p => p.Items)
            .HasForeignKey(e => e.ProductPackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany(p => p.PackItems)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
