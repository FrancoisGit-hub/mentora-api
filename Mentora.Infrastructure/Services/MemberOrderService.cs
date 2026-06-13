using System.Text;
using System.Text.Json;
using Mentora.Core.DTOs.Member;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class MemberOrderService(MentoraDbContext db) : IMemberOrderService
{
    // ── List (paginated) ───────────────────────────────────────────────────────

    public async Task<OrderListResponseDto> ListAsync(
        Guid memberId, string? statusFilter, int limit, string? cursor, CancellationToken ct)
    {
        if (limit is < 1 or > 100)
            throw new InvalidOperationException($"limit must be between 1 and 100 (got {limit}).");

        OrderStatus? effectiveStatus = null;
        if (statusFilter is not null)
        {
            try { effectiveStatus = EnumMappings.OrderStatusMapping.Parse(statusFilter); }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidOperationException(
                    $"Invalid status '{statusFilter}'. Accepted values: PENDING, PAID, EXPIRED, FAILED.");
            }
        }

        DateTime? cursorDate    = null;
        Guid?     cursorOrderId = null;
        if (cursor is not null)
        {
            var decoded = DecodeCursor(cursor);
            cursorDate    = decoded.CreatedDate;
            cursorOrderId = decoded.OrderId;
        }

        // Single query: fetch limit+1 to know whether a next page exists.
        // Items.Count is translated by EF Core to a COUNT(*) subquery — no N+1.
        var rawPage = await db.Orders
            .Where(o => o.MemberId == memberId)
            .Where(o => !effectiveStatus.HasValue || o.Status == effectiveStatus.Value)
            .Where(o => cursorDate == null || o.CreatedDate < cursorDate.Value)
            .OrderByDescending(o => o.CreatedDate)
            .ThenByDescending(o => o.OrderId)
            .Take(limit + 1)
            .Select(o => new
            {
                o.OrderId,
                o.Status,
                o.TotalEuros,
                ItemCount = o.Items.Count,
                o.CreatedDate,
                o.PaidAt
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var hasNext  = rawPage.Count > limit;
        var pageRows = hasNext ? rawPage.Take(limit) : rawPage;

        var orders = pageRows.Select(o => new OrderSummaryDto(
            OrderId:     o.OrderId,
            Status:      EnumMappings.OrderStatusMapping.ToWire(o.Status),
            TotalEuros:  o.TotalEuros,
            ItemCount:   o.ItemCount,
            CreatedDate: o.CreatedDate,
            PaidAt:      o.PaidAt)).ToList();

        string? nextCursor = null;
        if (hasNext && orders.Count > 0)
        {
            var last = orders[^1];
            nextCursor = EncodeCursor(last.CreatedDate, last.OrderId);
        }

        return new OrderListResponseDto(orders, nextCursor);
    }

    // ── Detail ─────────────────────────────────────────────────────────────────

    public async Task<OrderDetailDto> GetDetailAsync(
        Guid memberId, Guid orderId, CancellationToken ct)
    {
        // Query 1: load order + items, enforcing membership ownership.
        // Filtering on BOTH orderId AND memberId means a foreign-member order returns null → 404,
        // which is identical to a non-existent order — no information leakage.
        var order = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.OrderId == orderId && o.MemberId == memberId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Order not found.");

        // Query 2: load vouchers tied to this order's item lines.
        var orderItemIds = order.Items.Select(i => i.OrderItemId).ToList();
        var vouchers = await db.SessionVouchers
            .Where(v => orderItemIds.Contains(v.OrderItemId))
            .OrderBy(v => v.CreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        // Index items by id for fast product-name lookup when mapping vouchers
        var itemsById = order.Items.ToDictionary(i => i.OrderItemId);

        var itemDtos = order.Items.Select(ToOrderItemDetailDto).ToList();

        var voucherDtos = vouchers.Select(v =>
        {
            var productName = itemsById.TryGetValue(v.OrderItemId, out var item)
                ? item.ProductName
                : "(unavailable)";
            return ToOrderVoucherDto(v, productName);
        }).ToList();

        return new OrderDetailDto(
            OrderId:           order.OrderId,
            CoachId:           order.CoachId,
            Status:            EnumMappings.OrderStatusMapping.ToWire(order.Status),
            TotalEuros:        order.TotalEuros,
            CreatedDate:       order.CreatedDate,
            UpdatedDate:       order.UpdatedDate,
            PaidAt:            order.PaidAt,
            StripeSessionId:   order.StripeSessionId,
            StripeCheckoutUrl: order.StripeCheckoutUrl,
            Items:             itemDtos,
            Vouchers:          voucherDtos);
    }

    // ── Mappers ────────────────────────────────────────────────────────────────

    private static OrderItemDetailDto ToOrderItemDetailDto(OrderItem i) =>
        new(
            OrderItemId:                i.OrderItemId,
            ProductId:                  i.ProductId,
            ProductName:                i.ProductName,
            PackId:                     i.PackId,
            Quantity:                   i.Quantity,
            OfferType:                  EnumMappings.OfferTypeMapping.ToWire(i.OfferType),
            DurationMinutes:            i.DurationMinutes,
            Sport:                      EnumMappings.SportMapping.ToWire(i.Sport),
            OriginalUnitPriceEuros:     i.OrderItemOriginalUnitPriceEuros,
            UnitPriceEuros:             i.UnitPriceEuros,
            PackDiscountPercentApplied: i.OrderItemPackDiscountPercentApplied,
            LineTotalEuros:             i.LineTotalEuros);

    private static OrderVoucherDto ToOrderVoucherDto(SessionVoucher v, string productName) =>
        new(
            VoucherId:         v.VoucherId,
            CoachId:           v.CoachId,
            OrderItemId:       v.OrderItemId,
            ProductId:         v.ProductId,
            ProductName:       productName,
            OfferType:         EnumMappings.OfferTypeMapping.ToWire(v.OfferType),
            DurationMinutes:   v.DurationMinutes,
            Sport:             EnumMappings.SportMapping.ToWire(v.Sport),
            Status:            EnumMappings.VoucherStatusMapping.ToWire(v.Status),
            ReservedSessionId: v.ReservedSessionId,
            CreatedDate:       v.CreatedDate,
            UpdatedDate:       v.UpdatedDate);

    // ── Cursor helpers ─────────────────────────────────────────────────────────

    private static string EncodeCursor(DateTime createdDate, Guid orderId)
    {
        var payload = JsonSerializer.Serialize(new CursorPayload(createdDate, orderId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static CursorPayload DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorPayload>(json)
                ?? throw new InvalidOperationException("Invalid cursor.");
        }
        catch (FormatException)    { throw new InvalidOperationException("Invalid cursor format."); }
        catch (JsonException)      { throw new InvalidOperationException("Invalid cursor format."); }
    }

    private record CursorPayload(DateTime CreatedDate, Guid OrderId);
}
