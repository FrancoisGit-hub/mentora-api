using Mentora.Core.Entities;
using Mentora.Core.Enums;

namespace Mentora.Infrastructure.Services;

/// <summary>
/// Shared voucher-generation logic: one <see cref="SessionVoucher"/> per unit of
/// <see cref="OrderItem.Quantity"/>, for every item on the order. Used by both the Stripe
/// webhook (member purchases) and the coach voucher-grant flow, so the two never diverge.
/// </summary>
internal static class VoucherGeneration
{
    public static List<SessionVoucher> GenerateForOrder(Order order)
    {
        var now      = DateTime.UtcNow;
        var vouchers = new List<SessionVoucher>();

        foreach (var item in order.Items)
        {
            for (var n = 0; n < item.Quantity; n++)
            {
                vouchers.Add(new SessionVoucher
                {
                    VoucherId         = Guid.NewGuid(),
                    OrderItemId       = item.OrderItemId,
                    MemberId          = order.MemberId,
                    CoachId           = order.CoachId,
                    ProductId         = item.ProductId,
                    OfferType         = item.OfferType,
                    DurationMinutes   = item.DurationMinutes,
                    Sport             = item.Sport,
                    Status            = VoucherStatus.Available,
                    ReservedSessionId = null,
                    CreatedDate       = now,
                    UpdatedDate       = now,
                });
            }
        }

        return vouchers;
    }
}
