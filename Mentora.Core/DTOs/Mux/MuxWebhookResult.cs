namespace Mentora.Core.DTOs.Mux;

/// <summary>Lot 12.3a — what the MUX webhook handler did with one request.</summary>
public enum MuxWebhookOutcome
{
    /// <summary>Signature check failed: 401, empty body.</summary>
    Rejected,
    /// <summary>Signature valid but the body is not a MUX event: 400.</summary>
    InvalidPayload,
    /// <summary>Event id already recorded: 200 { duplicate: true }, nothing written.</summary>
    Duplicate,
    /// <summary>Event recorded as PROCESSED.</summary>
    Processed,
    /// <summary>Event recorded as IGNORED.</summary>
    Ignored,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Reason">Rejected only: missing, format, stale or signature.</param>
/// <param name="EventType">MUX event type, when parsed.</param>
/// <param name="Result">Short outcome text (no secret).</param>
public record MuxWebhookResult(MuxWebhookOutcome Outcome, string? Reason, string? EventType, string? Result);
