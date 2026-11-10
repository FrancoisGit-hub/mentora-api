using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

/// <summary>
/// Lot 12.3a — trace and idempotence of the MUX webhook (one row per MUX event id). The payload is
/// archived as raw text, exactly as received and verified.
/// </summary>
public class MuxWebhookEvent
{
    public Guid MuxWebhookEventId { get; set; }

    /// <summary>MUX event id (payload "id"), unique.</summary>
    public string MuxWebhookEventMuxId { get; set; } = null!;

    public string MuxWebhookEventType { get; set; } = null!;

    /// <summary>Payload "object.id" (upload or asset id), when present.</summary>
    public string? MuxWebhookEventObjectId { get; set; }

    public MuxWebhookEventStatus MuxWebhookEventStatus { get; set; }

    /// <summary>Short outcome, for example "video &lt;id&gt; READY" or "no matching video".</summary>
    public string? MuxWebhookEventResult { get; set; }

    public string MuxWebhookEventPayload { get; set; } = null!;

    public DateTime MuxWebhookEventCreatedDate { get; set; }
}
