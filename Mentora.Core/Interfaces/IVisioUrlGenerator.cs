namespace Mentora.Core.Interfaces;

public interface IVisioUrlGenerator
{
    /// <summary>Returns a random Jitsi room URL (used by session scheduling).</summary>
    string Generate();

    /// <summary>
    /// Returns a deterministic Jitsi URL for a given conversation ID.
    /// Used as the auto-fallback when CONVERSATION_VISIO_URL is null (Lot 3.4+).
    /// </summary>
    string GenerateForConversation(Guid conversationId);
}
