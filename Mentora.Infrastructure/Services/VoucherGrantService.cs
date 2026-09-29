using FluentValidation;
using Mentora.Core.DTOs.Voucher;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services;

public class VoucherGrantService(
    MentoraDbContext db,
    IValidator<VoucherGrantRequest> validator,
    ILogger<VoucherGrantService> logger) : IVoucherGrantService
{
    public async Task<VoucherGrantResponse> GrantAsync(
        Guid coachId, Guid memberId, VoucherGrantRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        // Member must be currently attached to this coach — 404, never 403.
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (!isLinked)
            throw new NotFoundException($"Member {memberId} not found.");

        // Product must belong to this coach and not be archived. Publication is NOT required —
        // a coach may grant sessions on a private (draft) offer.
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == request.ProductId && p.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Product {request.ProductId} not found.");

        if (product.ProductStatus == ProductStatus.Archived)
            throw new NotFoundException($"Product {request.ProductId} not found.");

        // VisioGroupe is declared for schema completeness only and rejected everywhere a product
        // could be assigned it (see OfferType.cs) — a Product can never actually carry it. Kept
        // as an explicit check because it is the one OfferType the purchase flow does not support.
        if (product.ProductOfferType == OfferType.VisioGroupe)
            throw new ConflictException(
                "Product type does not generate vouchers.",
                new { reason = "PRODUCT_NOT_ELIGIBLE" });

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            OrderId           = Guid.NewGuid(),
            MemberId          = memberId,
            CoachId           = coachId,
            Status            = OrderStatus.Paid,
            TotalEuros        = 0m,
            StripeSessionId   = null,
            StripeCheckoutUrl = null,
            Origin            = PurchaseOrigin.CoachGrant,
            GrantNote         = note,
            PaidAt            = now,
            CreatedDate       = now,
            UpdatedDate       = now,
        };

        var orderItem = new OrderItem
        {
            OrderItemId                     = Guid.NewGuid(),
            OrderId                         = order.OrderId,
            ProductId                       = product.ProductId,
            PackId                          = null,
            ProductName                     = product.ProductName,
            OrderItemOriginalUnitPriceEuros = product.ProductPriceEuros,
            UnitPriceEuros                  = 0m,
            OrderItemPackDiscountPercentApplied = null,
            Quantity                        = request.Quantity,
            LineTotalEuros                  = 0m,
            OfferType                       = product.ProductOfferType,
            DurationMinutes                 = product.ProductDurationMinutes,
            Sport                           = product.ProductSport,
        };
        order.Items.Add(orderItem);

        var vouchers = VoucherGeneration.GenerateForOrder(order);

        db.Orders.Add(order);
        db.OrderItems.Add(orderItem);
        db.SessionVouchers.AddRange(vouchers);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Coach {CoachId} granted {VoucherCount} voucher(s) on product {ProductId} to member {MemberId}.",
            coachId, vouchers.Count, product.ProductId, memberId);

        return new VoucherGrantResponse(
            PurchaseId:   order.OrderId,
            MemberId:     memberId,
            ProductId:    product.ProductId,
            Quantity:     request.Quantity,
            VoucherCount: vouchers.Count,
            VoucherIds:   vouchers.Select(v => v.VoucherId).ToList(),
            Origin:       EnumMappings.PurchaseOriginMapping.ToWire(order.Origin),
            GrantedDate:  now);
    }
}
