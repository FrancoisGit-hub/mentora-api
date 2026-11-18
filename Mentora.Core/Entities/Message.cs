using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Message
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string MessageContent { get; set; } = "";
    public MessageSenderType MessageSenderType { get; set; }
    public Guid MessageSenderId { get; set; }
    public bool MessageIsRead { get; set; }
    public DateTime MessageSentDate { get; set; }
    public DateTime? MessageReadDate { get; set; }

    /// <summary>Lot 13.2: kind of message. Text for every message written before the lot.</summary>
    public MessageType MessageType { get; set; } = MessageType.Text;

    /// <summary>Lot 13.2: relative storage path of the audio file (never exposed on the wire). Null for a text message.</summary>
    public string? MessageAudioPath { get; set; }

    /// <summary>Lot 13.2: content type of the stored audio file ("audio/aac" or "audio/mp4"). Null for a text message.</summary>
    public string? MessageAudioContentType { get; set; }

    /// <summary>Lot 13.2: duration of the audio in milliseconds. Null for a text message.</summary>
    public int? MessageAudioDurationMs { get; set; }

    /// <summary>Lot 13.2: size of the stored audio file in bytes. Null for a text message.</summary>
    public int? MessageAudioSizeBytes { get; set; }

    /// <summary>Lot 13.2: waveform bars (jsonb array of integers). Null for a text message.</summary>
    public List<int>? MessageAudioWaveform { get; set; }

    public Conversation Conversation { get; set; } = null!;
}
