using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("ORDERS");

        builder.HasKey(e => e.OrderId);
        builder.Property(e => e.OrderId)
            .HasColumnName("ORDER_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MemberId)
            .HasColumnName("ORDER_MEMBER_ID")
            .IsRequired();

        builder.Property(e => e.CoachId)
            .HasColumnName("ORDER_COACH_ID")
            .IsRequired();

        var statusConverter = new ValueConverter<OrderStatus, string>(
            v => OrderStatusToDb(v),
            v => OrderStatusFromDb(v));

        builder.Property(e => e.Status)
            .HasColumnName("ORDER_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'PENDING'");

        builder.Property(e => e.TotalEuros)
            .HasColumnName("ORDER_TOTAL_EUROS")
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(e => e.StripeSessionId)
            .HasColumnName("ORDER_STRIPE_SESSION_ID")
            .HasMaxLength(255);

        builder.Property(e => e.StripeCheckoutUrl)
            .HasColumnName("ORDER_STRIPE_CHECKOUT_URL")
            .HasColumnType("text");

        builder.Property(e => e.CreatedDate)
            .HasColumnName("ORDER_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.UpdatedDate)
            .HasColumnName("ORDER_UPDATED_DATE")
            .IsRequired();

        builder.Property(e => e.PaidAt)
            .HasColumnName("ORDER_PAID_AT");

        // Unique partial index — only one row per Stripe session id, nulls excluded
        builder.HasIndex(e => e.StripeSessionId)
            .IsUnique()
            .HasFilter("\"ORDER_STRIPE_SESSION_ID\" IS NOT NULL")
            .HasDatabaseName("IX_ORDERS_STRIPE_SESSION_ID_UNIQUE");

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Coach)
            .WithMany()
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string OrderStatusToDb(OrderStatus v)
    {
        if (v == OrderStatus.Pending) return "PENDING";
        if (v == OrderStatus.Paid)    return "PAID";
        if (v == OrderStatus.Expired) return "EXPIRED";
        if (v == OrderStatus.Failed)  return "FAILED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OrderStatus value.");
    }

    private static OrderStatus OrderStatusFromDb(string v)
    {
        if (v == "PENDING") return OrderStatus.Pending;
        if (v == "PAID")    return OrderStatus.Paid;
        if (v == "EXPIRED") return OrderStatus.Expired;
        if (v == "FAILED")  return OrderStatus.Failed;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown OrderStatus DB value.");
    }
}
