namespace Mentora.Core.Entities;

public class Conversation
{
    public Guid ConversationId { get; set; }
    public Guid MemberId { get; set; }
    public Guid CoachId { get; set; }
    public DateTime ConversationCreatedDate { get; set; }
    public DateTime? ConversationLastMessageDate { get; set; }

    public Member Member { get; set; } = null!;
    public Coach Coach { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
