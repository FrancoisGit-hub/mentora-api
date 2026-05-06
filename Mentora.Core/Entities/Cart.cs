namespace Mentora.Core.Entities;

public class Cart
{
    public Guid CartId { get; set; }

    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}
