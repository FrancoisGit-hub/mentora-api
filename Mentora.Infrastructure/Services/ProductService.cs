using FluentValidation;
using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class ProductService(
    MentoraDbContext db,
    IValidator<ProductRequest> validator) : IProductService
{
    public async Task<List<ProductResponse>> GetByCoachAsync(Guid coachId)
    {
        return await db.Products
            .Where(p => p.CoachId == coachId && p.ProductStatus != ProductStatus.Archived)
            .OrderByDescending(p => p.ProductCreatedDate)
            .Select(p => ToResponse(p))
            .ToListAsync();
    }

    public async Task<ProductResponse> GetByIdAsync(Guid productId, Guid coachId)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.CoachId == coachId)
            ?? throw new NotFoundException($"Product {productId} not found.");

        return ToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(ProductRequest request, Guid coachId)
    {
        await validator.ValidateAndThrowAsync(request);

        // Verify the OfferProgram belongs to this coach and is active
        var programExists = await db.OfferPrograms.AnyAsync(p =>
            p.OfferProgramId == request.OfferProgramId &&
            p.CoachId == coachId &&
            p.OfferProgramIsActive);

        if (!programExists)
            throw new NotFoundException($"OfferProgram {request.OfferProgramId} not found.");

        var now = DateTime.UtcNow;
        var product = new Product
        {
            ProductName            = request.Name.Trim(),
            ProductDescription     = request.Description?.Trim(),
            ProductOfferType       = EnumMappings.OfferTypeMapping.Parse(request.OfferType),
            ProductOfferNature     = NormalizeOfferNature(request.OfferNature),
            ProductDurationMinutes = request.DurationMinutes,
            ProductPriceEuros      = request.PriceEuros,
            ProductSport           = EnumMappings.SportMapping.Parse(request.Sport),
            ProductLocation        = request.Location?.Trim(),
            ProductTags            = NormalizeTags(request.Tags),
            ProductStatus          = ProductStatus.Draft,
            ProductCreatedDate     = now,
            ProductUpdatedDate     = now,
            OfferProgramId         = request.OfferProgramId,
            CoachId                = coachId
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(Guid productId, ProductRequest request, Guid coachId)
    {
        await validator.ValidateAndThrowAsync(request);

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.CoachId == coachId)
            ?? throw new NotFoundException($"Product {productId} not found.");

        if (product.ProductStatus == ProductStatus.Archived)
            throw new ConflictException("An archived product cannot be updated.");

        // Verify the new OfferProgram belongs to this coach and is active
        var programExists = await db.OfferPrograms.AnyAsync(p =>
            p.OfferProgramId == request.OfferProgramId &&
            p.CoachId == coachId &&
            p.OfferProgramIsActive);

        if (!programExists)
            throw new NotFoundException($"OfferProgram {request.OfferProgramId} not found.");

        product.ProductName            = request.Name.Trim();
        product.ProductDescription     = request.Description?.Trim();
        product.ProductOfferType       = EnumMappings.OfferTypeMapping.Parse(request.OfferType);
        product.ProductOfferNature     = NormalizeOfferNature(request.OfferNature);
        product.ProductDurationMinutes = request.DurationMinutes;
        product.ProductPriceEuros      = request.PriceEuros;
        product.ProductSport           = EnumMappings.SportMapping.Parse(request.Sport);
        product.ProductLocation        = request.Location?.Trim();
        product.ProductTags            = NormalizeTags(request.Tags);
        product.OfferProgramId         = request.OfferProgramId;
        product.ProductUpdatedDate     = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task DeleteAsync(Guid productId, Guid coachId)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.CoachId == coachId)
            ?? throw new NotFoundException($"Product {productId} not found.");

        if (product.ProductStatus == ProductStatus.Archived)
            throw new ConflictException("Product is already archived.");

        if (product.ProductStatus == ProductStatus.Published)
        {
            // Published → Archived (soft delete)
            product.ProductStatus      = ProductStatus.Archived;
            product.ProductUpdatedDate = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return;
        }

        // Draft → hard delete, but only if product is not referenced by any pack
        var inPack = await db.ProductPackItems
            .AnyAsync(i => i.ProductId == productId);

        if (inPack)
            throw new ConflictException("Product is used in one or more packs and cannot be deleted. Remove it from all packs first.");

        db.Products.Remove(product);
        await db.SaveChangesAsync();
    }

    public async Task<ProductResponse> PublishAsync(Guid productId, Guid coachId, CancellationToken ct)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Product {productId} not found.");

        if (product.ProductStatus == ProductStatus.Published)
            throw new ConflictException("Already published.");

        if (product.ProductStatus == ProductStatus.Archived)
            throw new ConflictException("Cannot publish an archived item.");

        product.ProductStatus      = ProductStatus.Published;
        product.ProductUpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToResponse(product);
    }

    public async Task<ProductResponse> UnpublishAsync(Guid productId, Guid coachId, CancellationToken ct)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.CoachId == coachId, ct)
            ?? throw new NotFoundException($"Product {productId} not found.");

        if (product.ProductStatus == ProductStatus.Draft)
            throw new ConflictException("Already in draft.");

        if (product.ProductStatus == ProductStatus.Archived)
            throw new ConflictException("Cannot unpublish an archived item.");

        // Rule B: block if any published pack still references this product
        var publishedPacks = await db.ProductPacks
            .Where(pp => pp.ProductPackStatus == ProductStatus.Published
                      && pp.Items.Any(i => i.ProductId == productId))
            .Select(pp => new { packId = pp.ProductPackId, name = pp.ProductPackName })
            .ToListAsync(ct);

        if (publishedPacks.Count > 0)
            throw new ConflictException(
                "Cannot unpublish product: it is included in published packs.",
                new { publishedPacks });

        product.ProductStatus      = ProductStatus.Draft;
        product.ProductUpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToResponse(product);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string NormalizeOfferNature(string? offerNature)
    {
        var trimmed = offerNature?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "CLASSIQUE" : trimmed.ToUpperInvariant();
    }

    private static List<string>? NormalizeTags(List<string>? tags)
    {
        if (tags is null or { Count: 0 }) return null;
        return tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
    }

    private static ProductResponse ToResponse(Product p) => new(
        p.ProductId,
        p.ProductName,
        p.ProductDescription,
        EnumMappings.OfferTypeMapping.ToWire(p.ProductOfferType),
        p.ProductOfferNature,
        p.ProductDurationMinutes,
        p.ProductPriceEuros,
        EnumMappings.SportMapping.ToWire(p.ProductSport),
        p.ProductLocation,
        p.ProductTags,
        EnumMappings.ProductStatusMapping.ToWire(p.ProductStatus),
        p.OfferProgramId,
        p.CoachId,
        p.ProductCreatedDate,
        p.ProductUpdatedDate
    );
}
