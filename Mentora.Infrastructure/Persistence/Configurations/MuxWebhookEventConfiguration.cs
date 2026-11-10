using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 12.3a — MUX_WEBHOOK_EVENTS. The status CHECK (PROCESSED / IGNORED) is raw SQL in the
/// Lot12_3a_MuxVideos migration.
/// </summary>
public class MuxWebhookEventConfiguration : IEntityTypeConfiguration<MuxWebhookEvent>
{
    public void Configure(EntityTypeBuilder<MuxWebhookEvent> builder)
    {
        builder.ToTable("MUX_WEBHOOK_EVENTS");

        builder.HasKey(e => e.MuxWebhookEventId);
        builder.Property(e => e.MuxWebhookEventId)
            .HasColumnName("MUX_WEBHOOK_EVENT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.MuxWebhookEventMuxId)
            .HasColumnName("MUX_WEBHOOK_EVENT_MUX_ID")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.MuxWebhookEventType)
            .HasColumnName("MUX_WEBHOOK_EVENT_TYPE")
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(e => e.MuxWebhookEventObjectId)
            .HasColumnName("MUX_WEBHOOK_EVENT_OBJECT_ID")
            .HasMaxLength(100);

        builder.Property(e => e.MuxWebhookEventStatus)
            .HasColumnName("MUX_WEBHOOK_EVENT_STATUS")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(new ValueConverter<MuxWebhookEventStatus, string>(
                v => StatusToDb(v),
                v => StatusFromDb(v)));

        builder.Property(e => e.MuxWebhookEventResult)
            .HasColumnName("MUX_WEBHOOK_EVENT_RESULT")
            .HasMaxLength(200);

        builder.Property(e => e.MuxWebhookEventPayload)
            .HasColumnName("MUX_WEBHOOK_EVENT_PAYLOAD")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.MuxWebhookEventCreatedDate)
            .HasColumnName("MUX_WEBHOOK_EVENT_CREATED_DATE")
            .IsRequired();

        builder.HasIndex(e => e.MuxWebhookEventMuxId)
            .IsUnique()
            .HasDatabaseName("UX_MUX_WEBHOOK_EVENTS_MUX_ID");

        builder.HasIndex(e => e.MuxWebhookEventCreatedDate)
            .HasDatabaseName("IX_MUX_WEBHOOK_EVENTS_CREATED_DATE");
    }

    private static string StatusToDb(MuxWebhookEventStatus v)
    {
        if (v == MuxWebhookEventStatus.Processed) return "PROCESSED";
        if (v == MuxWebhookEventStatus.Ignored)   return "IGNORED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MuxWebhookEventStatus value.");
    }

    private static MuxWebhookEventStatus StatusFromDb(string v)
    {
        if (v == "PROCESSED") return MuxWebhookEventStatus.Processed;
        if (v == "IGNORED")   return MuxWebhookEventStatus.Ignored;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown MuxWebhookEventStatus DB value.");
    }
}
