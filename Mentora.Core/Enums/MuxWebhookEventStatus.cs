namespace Mentora.Core.Enums;

/// <summary>
/// Lot 12.3a — outcome of a received MUX webhook event.
/// Persisted as VARCHAR(10) UPPERCASE: PROCESSED / IGNORED.
/// </summary>
public enum MuxWebhookEventStatus
{
    Processed,
    Ignored
}
