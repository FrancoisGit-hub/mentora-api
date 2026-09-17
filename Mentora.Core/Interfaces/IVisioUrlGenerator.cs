namespace Mentora.Core.Interfaces;

public interface IVisioUrlGenerator
{
    /// <summary>Returns a random Jitsi room URL (used by session scheduling).</summary>
    string Generate();

    /// <summary>
    /// Returns a deterministic, permanent Jitsi URL for a given conversation ID. This room is not
    /// time-boxed — it never expires and has no joinable window. Distinct from per-session video
    /// calls, which since Lot 8 go through LiveKit (POST .../sessions/{sessionId}/visio-token),
    /// not Jitsi.
    /// </summary>
    string GenerateForConversation(Guid conversationId);
}
