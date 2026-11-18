namespace Mentora.Core.Enums;

/// <summary>
/// Lot 13.2: kind of a chat message. Stored in MESSAGES.MESSAGE_TYPE as "TEXT" / "AUDIO" and
/// serialized on the wire (REST and SignalR) as "TEXT" / "AUDIO" by the SnakeCaseUpper enum converter.
/// </summary>
public enum MessageType
{
    /// <summary>Text message: the content carries the text, every audio field is null.</summary>
    Text,

    /// <summary>Voice message: the content is empty, the audio fields describe the stored file.</summary>
    Audio
}
