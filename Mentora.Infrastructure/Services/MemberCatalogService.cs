using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class MemberCatalogService(MentoraDbContext db) : IMemberCatalogService
{
    public async Task<MemberCatalogResponseDto> GetCatalogAsync(
        Guid memberId,
        Guid coachId,
        OfferType? offerType,
        Guid? offerProgramId,
        CancellationToken ct)
    {
        // 1 — coach existence (single query; also provides coach info for the DTO)
        var coach = await db.Coaches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CoachId == coachId, ct)
            ?? throw new NotFoundException("Coach not found.");

        // 2 — member-coach link check
        var isLinked = await db.MemberCoaches
            .AnyAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);

        if (!isLinked)
            throw new ForbiddenException("You are not linked to this coach.");

        // 3 — standalone PUBLISHED products (with offer-program name)
        var productsQuery = db.Products
            .AsNoTracking()
            .Include(p => p.OfferProgram)
            .Where(p => p.CoachId == coachId && p.ProductStatus == ProductStatus.Published);

        if (offerType.HasValue)
            productsQuery = productsQuery.Where(p => p.ProductOfferType == offerType.Value);

        if (offerProgramId.HasValue)
            productsQuery = productsQuery.Where(p => p.OfferProgramId == offerProgramId.Value);

        var products = await productsQuery
            .OrderBy(p => p.ProductCreatedDate)
            .ToListAsync(ct);

        // 4 — PUBLISHED packs with full composition (items NOT filtered on product status)
        var packs = await db.ProductPacks
            .AsNoTracking()
            .Include(pk => pk.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(p => p.OfferProgram)
            .Where(pk => pk.CoachId == coachId && pk.ProductPackStatus == ProductStatus.Published)
            .OrderBy(pk => pk.ProductPackCreatedDate)
            .ToListAsync(ct);

        // Pack-level in-memory filter (Option B: keep pack if ≥1 item satisfies BOTH conditions simultaneously)
        // V1 catalog sizes are small; DB-side evaluation of per-item predicates would require complex joins.
        if (offerType.HasValue || offerProgramId.HasValue)
        {
            packs = packs.Where(pk => pk.Items.Any(item =>
                (!offerType.HasValue    || item.Product.ProductOfferType == offerType.Value) &&
                (!offerProgramId.HasValue || item.Product.OfferProgramId  == offerProgramId.Value)
            )).ToList();
        }

        // 5 — map to DTOs
        var coachDto = new CatalogCoachDto(
            Id:        coach.CoachId,
            FirstName: coach.CoachFirstName,
            LastName:  coach.CoachLastName,
            // TODO: add COACH_BIO and COACH_AVATAR_URL columns to COACHES table and map here
            Bio:       null,
            AvatarUrl: null
        );

        var productDtos = products.Select(MapProduct).ToList();

        var packDtos = packs.Select(pk => new PackCatalogDto(
            Id:          pk.ProductPackId,
            Name:        pk.ProductPackName,
            Description: pk.ProductPackDescription,
            PriceEuros:  pk.ProductPackPriceEuros,
            Items:       pk.Items
                           .OrderBy(i => i.ProductPackItemId)
                           .Select(i => new PackItemCatalogDto(
                               PackItemId: i.ProductPackItemId,
                               Quantity:   i.ProductPackItemQuantity,
                               Product:    MapProduct(i.Product)
                           ))
                           .ToList()
        )).ToList();

        return new MemberCatalogResponseDto(coachDto, productDtos, packDtos);
    }

    private static ProductCatalogDto MapProduct(Product p) => new(
        Id:              p.ProductId,
        Name:            p.ProductName,
        Description:     p.ProductDescription,
        PriceEuros:      p.ProductPriceEuros,
        OfferType:       p.ProductOfferType,
        DurationMinutes: p.ProductDurationMinutes,
        OfferProgramId:  p.OfferProgramId,
        OfferProgramName: p.OfferProgram.OfferProgramName,
        // TODO: add PRODUCT_MAX_PARTICIPANTS column and map p.ProductMaxParticipants when OfferType == PresentielGroupe
        MaxParticipants: null
    );
}
