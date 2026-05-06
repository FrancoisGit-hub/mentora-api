using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Order
{
    public Guid OrderId { get; set; }

    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalEuros { get; set; }

    public string? StripeSessionId { get; set; }
    public string? StripeCheckoutUrl { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
