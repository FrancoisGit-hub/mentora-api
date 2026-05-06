namespace Mentora.Core.DTOs.Stripe;

public record CreateStripeSessionRequest(
    Guid OrderId,
    Guid MemberId,
    Guid CoachId,
    decimal TotalEuros,
    IReadOnlyList<CreateStripeSessionLineItem> LineItems);
