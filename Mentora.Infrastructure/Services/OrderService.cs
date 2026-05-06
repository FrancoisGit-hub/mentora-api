using Mentora.Core.DTOs.Order;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class OrderService(MentoraDbContext db) : IOrderService
{
    public async Task<IReadOnlyList<OrderResponse>> ListForMemberAsync(
        Guid memberId, OrderStatus? statusFilter, CancellationToken ct)
    {
        var orders = await db.Orders
            .Include(o => o.Coach)
            .Include(o => o.Items)
            .Where(o => o.MemberId == memberId)
            .Where(o => !statusFilter.HasValue || o.Status == statusFilter.Value)
            .OrderByDescending(o => o.CreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return orders.Select(ToOrderResponse).ToList();
    }

    public async Task<OrderResponse> GetForMemberAsync(
        Guid memberId, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Coach)
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MemberId == memberId && o.OrderId == orderId, ct)
            ?? throw new NotFoundException("Order not found.");

        return ToOrderResponse(order);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static OrderResponse ToOrderResponse(Order o)
    {
        var items = o.Items.Select(ToOrderItemResponse).ToList();
        return new OrderResponse(
            OrderId:          o.OrderId,
            CoachId:          o.CoachId,
            CoachDisplayName: $"{o.Coach.CoachFirstName} {o.Coach.CoachLastName}".Trim(),
            Status:           EnumMappings.OrderStatusMapping.ToWire(o.Status),
            TotalEuros:       o.TotalEuros,
            // Expose checkout URL only while the order is still actionable
            StripeCheckoutUrl: o.Status == OrderStatus.Pending ? o.StripeCheckoutUrl : null,
            CreatedDate:      o.CreatedDate,
            UpdatedDate:      o.UpdatedDate,
            PaidAt:           o.PaidAt,
            Items:            items);
    }

    private static OrderItemResponse ToOrderItemResponse(OrderItem i) =>
        new(
            OrderItemId:               i.OrderItemId,
            ProductId:                 i.ProductId,
            PackId:                    i.PackId,
            ProductName:               i.ProductName,
            OriginalUnitPriceEuros:    i.OrderItemOriginalUnitPriceEuros,
            UnitPriceEuros:            i.UnitPriceEuros,
            PackDiscountPercentApplied: i.OrderItemPackDiscountPercentApplied,
            Quantity:                  i.Quantity,
            LineTotalEuros:            i.LineTotalEuros,
            OfferType:                 EnumMappings.OfferTypeMapping.ToWire(i.OfferType),
            DurationMinutes:           i.DurationMinutes,
            Sport:                     EnumMappings.SportMapping.ToWire(i.Sport));
}
