namespace Mentora.Core.DTOs.Stripe;

public record SimulateWebhookRequest(
    string EventType,
    string SessionId);
