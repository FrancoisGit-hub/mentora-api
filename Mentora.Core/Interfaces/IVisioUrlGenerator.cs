namespace Mentora.Core.Interfaces;

public interface IVisioUrlGenerator
{
    /// <summary>Returns a random Jitsi room URL (used by session scheduling).</summary>
    string Generate();

    /// <summary>
    /// Returns a deterministic, permanent Jitsi URL for a given conversation ID. This room is not
    /// time-boxed — unlike per-session VISIO URLs, it never expires and has no joinable window.
    /// </summary>
    string GenerateForConversation(Guid conversationId);
}
