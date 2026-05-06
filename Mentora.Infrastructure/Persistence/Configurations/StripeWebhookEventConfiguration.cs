using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.ToTable("STRIPE_WEBHOOK_EVENTS");

        // PK is the Stripe-assigned event id (e.g. "evt_xxx") — not a UUID
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId)
            .HasColumnName("EVENT_ID")
            .HasMaxLength(255);

        builder.Property(e => e.EventType)
            .HasColumnName("EVENT_TYPE")
            .IsRequired()
            .HasMaxLength(100);

        // Stored as jsonb; EnableDynamicJson() is already wired in the data source builder
        builder.Property(e => e.Payload)
            .HasColumnName("EVENT_PAYLOAD")
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.ReceivedAt)
            .HasColumnName("EVENT_RECEIVED_AT")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("EVENT_PROCESSED_AT");

        var statusConverter = new ValueConverter<StripeEventStatus, string>(
            v => StripeEventStatusToDb(v),
            v => StripeEventStatusFromDb(v));

        builder.Property(e => e.Status)
            .HasColumnName("EVENT_STATUS")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'RECEIVED'");

        builder.Property(e => e.ProcessingError)
            .HasColumnName("EVENT_PROCESSING_ERROR")
            .HasColumnType("text");
    }

    private static string StripeEventStatusToDb(StripeEventStatus v)
    {
        if (v == StripeEventStatus.Received)  return "RECEIVED";
        if (v == StripeEventStatus.Processed) return "PROCESSED";
        if (v == StripeEventStatus.Failed)    return "FAILED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown StripeEventStatus value.");
    }

    private static StripeEventStatus StripeEventStatusFromDb(string v)
    {
        if (v == "RECEIVED")  return StripeEventStatus.Received;
        if (v == "PROCESSED") return StripeEventStatus.Processed;
        if (v == "FAILED")    return StripeEventStatus.Failed;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown StripeEventStatus DB value.");
    }
}
