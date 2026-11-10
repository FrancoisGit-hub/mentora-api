using Mentora.Core.DTOs.Mux;

namespace Mentora.Core.Interfaces;

/// <summary>
/// Lot 12.3a — verifies (HMAC SHA256 over the raw bytes, before any JSON parsing), records and applies a
/// MUX webhook event. Never calls MUX.
/// </summary>
public interface IMuxWebhookService
{
    Task<MuxWebhookResult> HandleAsync(byte[] rawBody, string? signatureHeader, CancellationToken ct);
}
