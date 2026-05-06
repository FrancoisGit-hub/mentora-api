using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class StripeWebhookEvent
{
    // PK is the Stripe event id (e.g. "evt_xxx") — not a UUID
    public string EventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    // Stored as jsonb; string keeps mapping simple given EnableDynamicJson() is in place
    public string Payload { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public StripeEventStatus Status { get; set; } = StripeEventStatus.Received;

    public string? ProcessingError { get; set; }
}
