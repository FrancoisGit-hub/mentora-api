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

    public Conversation Conversation { get; set; } = null!;
}
