using FluentValidation;
using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class ProductPackService(
    MentoraDbContext db,
    IValidator<ProductPackRequest> validator) : IProductPackService
{
    public async Task<List<ProductPackResponse>> GetByCoachAsync(Guid coachId)
    {
        var packs = await db.ProductPacks
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .Where(p => p.CoachId == coachId && p.ProductPackStatus != ProductStatus.Archived)
            .OrderByDescending(p => p.ProductPackCreatedDate)
            .ToListAsync();

        return packs.Select(ToResponse).ToList();
    }

    public async Task<ProductPackResponse> GetByIdAsync(Guid packId, Guid coachId)
    {
        var pack = await db.ProductPacks
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.ProductPackId == packId && p.CoachId == coachId)
            ?? throw new NotFoundException($"ProductPack {packId} not found.");

        return ToResponse(pack);
    }

    public async Task<ProductPackResponse> CreateAsync(ProductPackRequest request, Guid coachId)
    {
        await validator.ValidateAndThrowAsync(request);

        // Verify all products exist, belong to this coach, and are not archived
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.ProductId) && p.CoachId == coachId)
            .ToListAsync();

        var missingIds = productIds.Except(products.Select(p => p.ProductId)).ToList();
        if (missingIds.Count > 0)
            throw new NotFoundException($"Products not found: {string.Join(", ", missingIds)}");

        var archivedProducts = products.Where(p => p.ProductStatus == ProductStatus.Archived).ToList();
        if (archivedProducts.Count > 0)
            throw new ConflictException($"Archived products cannot be added to a pack: {string.Join(", ", archivedProducts.Select(p => p.ProductId))}");

        var now = DateTime.UtcNow;
        var pack = new ProductPack
        {
            ProductPackName        = request.Name.Trim(),
            ProductPackDescription = request.Description?.Trim(),
            ProductPackPriceEuros  = request.PriceEuros,
            ProductPackStatus      = ProductStatus.Draft,
            ProductPackCreatedDate = now,
            ProductPackUpdatedDate = now,
            CoachId                = coachId,
            Items = request.Items.Select(i => new ProductPackItem
            {
                ProductPackItemQuantity = i.Quantity,
                ProductId               = i.ProductId
            }).ToList()
        };

        db.ProductPacks.Add(pack);
        await db.SaveChangesAsync();

        // Reload with navigation properties for response
        await db.Entry(pack).Collection(p => p.Items).Query()
            .Include(i => i.Product)
            .LoadAsync();

        return ToResponse(pack);
    }

    public async Task<ProductPackResponse> UpdateAsync(Guid packId, ProductPackRequest request, Guid coachId)
    {
        await validator.ValidateAndThrowAsync(request);

        var pack = await db.ProductPacks
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.ProductPackId == packId && p.CoachId == coachId)
            ?? throw new NotFoundException($"ProductPack {packId} not found.");

        if (pack.ProductPackStatus == ProductStatus.Archived)
            throw new ConflictException("An archived pack cannot be updated.");

        // Verify all products exist, belong to this coach, and are not archived
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.ProductId) && p.CoachId == coachId)
            .ToListAsync();

        var missingIds = productIds.Except(products.Select(p => p.ProductId)).ToList();
        if (missingIds.Count > 0)
            throw new NotFoundException($"Products not found: {string.Join(", ", missingIds)}");

        var archivedProducts = products.Where(p => p.ProductStatus == ProductStatus.Archived).ToList();
        if (archivedProducts.Count > 0)
            throw new ConflictException($"Archived products cannot be added to a pack: {string.Join(", ", archivedProducts.Select(p => p.ProductId))}");

        // Replace items — remove all existing, add new set
        db.ProductPackItems.RemoveRange(pack.Items);

        pack.ProductPackName        = request.Name.Trim();
        pack.ProductPackDescription = request.Description?.Trim();
        pack.ProductPackPriceEuros  = request.PriceEuros;
        pack.ProductPackUpdatedDate = DateTime.UtcNow;
        pack.Items = request.Items.Select(i => new ProductPackItem
        {
            ProductPackItemQuantity = i.Quantity,
            ProductId               = i.ProductId,
            ProductPackId           = packId
        }).ToList();

        await db.SaveChangesAsync();

        // Reload with navigation properties for response
        await db.Entry(pack).Collection(p => p.Items).Query()
            .Include(i => i.Product)
            .LoadAsync();

        return ToResponse(pack);
    }

    public async Task DeleteAsync(Guid packId, Guid coachId)
    {
        var pack = await db.ProductPacks
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.ProductPackId == packId && p.CoachId == coachId)
            ?? throw new NotFoundException($"ProductPack {packId} not found.");

        if (pack.ProductPackStatus == ProductStatus.Archived)
            throw new ConflictException("Pack is already archived.");

        if (pack.ProductPackStatus == ProductStatus.Published)
        {
            // Published → Archived (soft delete)
            pack.ProductPackStatus      = ProductStatus.Archived;
            pack.ProductPackUpdatedDate = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return;
        }

        // Draft → hard delete (cascade removes items)
        db.ProductPacks.Remove(pack);
        await db.SaveChangesAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ProductPackResponse ToResponse(ProductPack p)
    {
        var items = p.Items.Select(i => new ProductPackItemResponse(
            i.ProductPackItemId,
            i.ProductId,
            i.Product?.ProductName ?? string.Empty,
            i.ProductPackItemQuantity
        )).ToList();

        var itemsTotalEuros = p.Items.Sum(
            i => (i.Product?.ProductPriceEuros ?? 0m) * i.ProductPackItemQuantity);

        return new(
            p.ProductPackId,
            p.ProductPackName,
            p.ProductPackDescription,
            p.ProductPackPriceEuros,
            itemsTotalEuros,
            EnumMappings.ProductStatusMapping.ToWire(p.ProductPackStatus),
            items,
            p.CoachId,
            p.ProductPackCreatedDate,
            p.ProductPackUpdatedDate
        );
    }
}
