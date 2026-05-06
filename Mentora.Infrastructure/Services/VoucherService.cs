using Mentora.Core.DTOs.Voucher;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class VoucherService(MentoraDbContext db) : IVoucherService
{
    public async Task<IReadOnlyList<VoucherResponse>> ListForMemberAsync(
        Guid memberId, VoucherStatus? statusFilter, Guid? coachIdFilter, CancellationToken ct)
    {
        var vouchers = await db.SessionVouchers
            .Where(v => v.MemberId == memberId)
            .Where(v => !statusFilter.HasValue  || v.Status  == statusFilter.Value)
            .Where(v => !coachIdFilter.HasValue || v.CoachId == coachIdFilter.Value)
            .OrderByDescending(v => v.CreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        if (vouchers.Count == 0)
            return [];

        // Batch-load coaches and products to avoid N+1
        var coachIds   = vouchers.Select(v => v.CoachId).Distinct().ToList();
        var productIds = vouchers.Select(v => v.ProductId).Distinct().ToList();

        var coaches = await db.Coaches
            .Where(c => coachIds.Contains(c.CoachId))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CoachId, ct);

        var products = await db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .AsNoTracking()
            .ToDictionaryAsync(p => p.ProductId, ct);

        return vouchers.Select(v =>
        {
            coaches.TryGetValue(v.CoachId, out var coach);
            products.TryGetValue(v.ProductId, out var product);
            return ToVoucherResponse(v, coach, product);
        }).ToList();
    }

    public async Task<VoucherResponse> GetForMemberAsync(
        Guid memberId, Guid voucherId, CancellationToken ct)
    {
        var voucher = await db.SessionVouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.MemberId == memberId && v.VoucherId == voucherId, ct)
            ?? throw new NotFoundException("Voucher not found.");

        var coach = await db.Coaches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CoachId == voucher.CoachId, ct);

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == voucher.ProductId, ct);

        return ToVoucherResponse(voucher, coach, product);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static VoucherResponse ToVoucherResponse(
        SessionVoucher v, Coach? coach, Product? product)
    {
        var displayName = coach is not null
            ? $"{coach.CoachFirstName} {coach.CoachLastName}".Trim()
            : "(unavailable)";

        return new VoucherResponse(
            VoucherId:        v.VoucherId,
            OrderItemId:      v.OrderItemId,
            CoachId:          v.CoachId,
            CoachDisplayName: displayName,
            ProductId:        v.ProductId,
            ProductName:      product?.ProductName ?? "(unavailable)",
            OfferType:        EnumMappings.OfferTypeMapping.ToWire(v.OfferType),
            DurationMinutes:  v.DurationMinutes,
            Sport:            EnumMappings.SportMapping.ToWire(v.Sport),
            Status:           EnumMappings.VoucherStatusMapping.ToWire(v.Status),
            ReservedSessionId: v.ReservedSessionId,
            CreatedDate:      v.CreatedDate,
            UpdatedDate:      v.UpdatedDate);
    }
}
