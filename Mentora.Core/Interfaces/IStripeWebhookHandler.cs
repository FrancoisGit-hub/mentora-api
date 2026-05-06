namespace Mentora.Core.Interfaces;

public interface IStripeWebhookHandler
{
    Task<StripeWebhookResult> HandleAsync(
        string eventId,
        string eventType,
        string rawPayload,
        string? sessionId,
        CancellationToken ct);
}

public record StripeWebhookResult(
    bool Idempotent,
    string Status);
