using Mentora.Core.DTOs.Member;
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
    public async Task<IReadOnlyList<MemberVouchersByCoachGroupDto>> ListForMemberAsync(
        Guid memberId, string? statusFilter, CancellationToken ct)
    {
        // Null → default to AVAILABLE; non-null must be a recognised wire value.
        VoucherStatus effectiveStatus;
        if (statusFilter is null)
        {
            effectiveStatus = VoucherStatus.Available;
        }
        else
        {
            try { effectiveStatus = EnumMappings.VoucherStatusMapping.Parse(statusFilter); }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidOperationException(
                    $"Invalid status '{statusFilter}'. Accepted values: AVAILABLE, RESERVED, CONSUMED.");
            }
        }

        var vouchers = await db.SessionVouchers
            .Include(v => v.OrderItem)
            .Where(v => v.MemberId == memberId && v.Status == effectiveStatus)
            .AsNoTracking()
            .ToListAsync(ct);

        if (vouchers.Count == 0)
            return [];

        var coachIds = vouchers.Select(v => v.CoachId).Distinct().ToHashSet();

        // Determine group order from MEMBER_COACHES.StartedAt ASC
        var memberCoachOrder = await db.MemberCoaches
            .Where(mc => mc.MemberId == memberId && coachIds.Contains(mc.CoachId))
            .OrderBy(mc => mc.StartedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var vouchersByCoach = vouchers
            .GroupBy(v => v.CoachId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(v => v.UpdatedDate)
                       .Select(ToVoucherDto)
                       .ToList());

        // Only emit groups that have at least one voucher; order follows StartedAt
        return memberCoachOrder
            .Where(mc => vouchersByCoach.ContainsKey(mc.CoachId))
            .Select(mc => new MemberVouchersByCoachGroupDto(
                CoachId:  mc.CoachId,
                Vouchers: vouchersByCoach[mc.CoachId]))
            .ToList();
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

    private static MemberVoucherDto ToVoucherDto(SessionVoucher v) =>
        new(
            VoucherId:         v.VoucherId,
            Status:            EnumMappings.VoucherStatusMapping.ToWire(v.Status),
            OfferType:         EnumMappings.OfferTypeMapping.ToWire(v.OfferType),
            DurationMinutes:   v.DurationMinutes,
            Sport:             EnumMappings.SportMapping.ToWire(v.Sport),
            ProductId:         v.ProductId,
            ProductName:       v.OrderItem.ProductName,
            ReservedSessionId: v.ReservedSessionId,
            CreatedDate:       v.CreatedDate,
            UpdatedDate:       v.UpdatedDate);

    private static VoucherResponse ToVoucherResponse(
        SessionVoucher v, Coach? coach, Product? product)
    {
        var displayName = coach is not null
            ? $"{coach.CoachFirstName} {coach.CoachLastName}".Trim()
            : "(unavailable)";

        return new VoucherResponse(
            VoucherId:         v.VoucherId,
            OrderItemId:       v.OrderItemId,
            CoachId:           v.CoachId,
            CoachDisplayName:  displayName,
            ProductId:         v.ProductId,
            ProductName:       product?.ProductName ?? "(unavailable)",
            OfferType:         EnumMappings.OfferTypeMapping.ToWire(v.OfferType),
            DurationMinutes:   v.DurationMinutes,
            Sport:             EnumMappings.SportMapping.ToWire(v.Sport),
            Status:            EnumMappings.VoucherStatusMapping.ToWire(v.Status),
            ReservedSessionId: v.ReservedSessionId,
            CreatedDate:       v.CreatedDate,
            UpdatedDate:       v.UpdatedDate);
    }
}
