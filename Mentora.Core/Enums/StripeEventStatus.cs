namespace Mentora.Core.Enums;

/// <summary>
/// Processing state of a received Stripe webhook event.
/// Persisted as VARCHAR(20) UPPERCASE: RECEIVED / PROCESSED / FAILED.
/// </summary>
public enum StripeEventStatus
{
    Received,
    Processed,
    Failed
}
