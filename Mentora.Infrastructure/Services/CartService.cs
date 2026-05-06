using FluentValidation;
using Mentora.Core.DTOs.Cart;
using Mentora.Core.DTOs.Stripe;
using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services;

public class CartService(
    MentoraDbContext db,
    IValidator<AddCartItemRequest> addItemValidator,
    IValidator<UpdateCartItemQuantityRequest> updateQuantityValidator,
    IStripeCheckoutService stripe,
    ILogger<CartService> logger) : ICartService
{
    // ── Public methods ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CartResponse>> ListAsync(Guid memberId, CancellationToken ct)
    {
        var carts = await db.Carts
            .Include(c => c.Coach)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .Include(c => c.Items).ThenInclude(i => i.Pack)
                .ThenInclude(p => p!.Items).ThenInclude(pi => pi.Product)
            .Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.UpdatedDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return carts.Select(c => ToCartResponse(c, c.Coach, memberId)).ToList();
    }

    public async Task<CartResponse> GetForCoachAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);
        var cart  = await LoadCartWithItems(memberId, coachId, ct);
        return ToCartResponse(cart, coach, memberId);
    }

    public async Task<CartResponse> AddItemAsync(
        Guid memberId, Guid coachId, AddCartItemRequest request, CancellationToken ct)
    {
        await addItemValidator.ValidateAndThrowAsync(request, ct);
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);

        if (request.ProductId.HasValue)
        {
            var product = await db.Products
                .FirstOrDefaultAsync(p => p.ProductId == request.ProductId.Value
                                       && p.CoachId == coachId, ct)
                ?? throw new NotFoundException("Product not found.");

            if (product.ProductStatus != ProductStatus.Published)
                throw new ConflictException(
                    "Product is not available for purchase.",
                    new { productId = product.ProductId,
                          status = EnumMappings.ProductStatusMapping.ToWire(product.ProductStatus) });
        }
        else
        {
            var pack = await db.ProductPacks
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.ProductPackId == request.PackId!.Value
                                       && p.CoachId == coachId, ct)
                ?? throw new NotFoundException("Pack not found.");

            if (pack.ProductPackStatus != ProductStatus.Published)
                throw new ConflictException(
                    "Pack is not available for purchase.",
                    new { packId = pack.ProductPackId,
                          status = EnumMappings.ProductStatusMapping.ToWire(pack.ProductPackStatus) });

            if (pack.Items.Count == 0)
                throw new ConflictException("Pack contains no items.");
        }

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);

        if (cart is null)
        {
            cart = new Cart
            {
                CartId      = Guid.NewGuid(),
                MemberId    = memberId,
                CoachId     = coachId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            };
            db.Carts.Add(cart);
        }

        var existingItem = cart.Items.FirstOrDefault(i =>
            (request.ProductId.HasValue && i.ProductId == request.ProductId) ||
            (request.PackId.HasValue   && i.PackId    == request.PackId));

        if (existingItem is not null)
        {
            var newQty = existingItem.Quantity + request.Quantity;
            if (newQty > 99)
                throw new ConflictException(
                    "Quantity exceeds maximum (99).",
                    new { currentQuantity = existingItem.Quantity, requested = request.Quantity });
            existingItem.Quantity = newQty;
        }
        else
        {
            db.CartItems.Add(new CartItem
            {
                CartItemId = Guid.NewGuid(),
                CartId     = cart.CartId,
                ProductId  = request.ProductId,
                PackId     = request.PackId,
                Quantity   = request.Quantity,
                AddedDate  = DateTime.UtcNow,
            });
        }

        cart.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToCartResponse(await LoadCartWithItems(memberId, coachId, ct), coach, memberId);
    }

    public async Task<CartResponse> UpdateItemQuantityAsync(
        Guid memberId, Guid coachId, Guid cartItemId,
        UpdateCartItemQuantityRequest request, CancellationToken ct)
    {
        await updateQuantityValidator.ValidateAndThrowAsync(request, ct);
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct)
            ?? throw new NotFoundException("Cart not found.");

        var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId)
            ?? throw new NotFoundException("Cart item not found.");

        item.Quantity    = request.Quantity;
        cart.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToCartResponse(await LoadCartWithItems(memberId, coachId, ct), coach, memberId);
    }

    public async Task<CartResponse> RemoveItemAsync(
        Guid memberId, Guid coachId, Guid cartItemId, CancellationToken ct)
    {
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct)
            ?? throw new NotFoundException("Cart not found.");

        var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId)
            ?? throw new NotFoundException("Cart item not found.");

        db.CartItems.Remove(item);
        cart.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToCartResponse(await LoadCartWithItems(memberId, coachId, ct), coach, memberId);
    }

    public async Task<CartResponse> ClearAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);

        if (cart is null)
            return ToCartResponse(null, coach, memberId);

        db.CartItems.RemoveRange(cart.Items);
        cart.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToCartResponse(await LoadCartWithItems(memberId, coachId, ct), coach, memberId);
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var coach = await EnsureMemberCoachLink(memberId, coachId, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var cart = await db.Carts
                .Include(c => c.Items).ThenInclude(i => i.Product)
                .Include(c => c.Items).ThenInclude(i => i.Pack)
                    .ThenInclude(p => p!.Items).ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);

            if (cart is null || cart.Items.Count == 0)
                throw new ConflictException("Cart is empty.");

            // Re-validate availability of every item at checkout time
            var unavailable = new List<object>();
            foreach (var cartItem in cart.Items)
            {
                if (cartItem.ProductId.HasValue)
                {
                    if (cartItem.Product is null || cartItem.Product.ProductStatus != ProductStatus.Published)
                        unavailable.Add(new
                        {
                            cartItemId = cartItem.CartItemId,
                            kind   = "PRODUCT",
                            name   = cartItem.Product?.ProductName ?? "(unknown)",
                            status = cartItem.Product is null
                                ? "UNKNOWN"
                                : EnumMappings.ProductStatusMapping.ToWire(cartItem.Product.ProductStatus)
                        });
                }
                else if (cartItem.PackId.HasValue)
                {
                    if (cartItem.Pack is null || cartItem.Pack.ProductPackStatus != ProductStatus.Published)
                    {
                        unavailable.Add(new
                        {
                            cartItemId = cartItem.CartItemId,
                            kind   = "PACK",
                            name   = cartItem.Pack?.ProductPackName ?? "(unknown)",
                            status = cartItem.Pack is null
                                ? "UNKNOWN"
                                : EnumMappings.ProductStatusMapping.ToWire(cartItem.Pack.ProductPackStatus)
                        });
                    }
                    else
                    {
                        foreach (var packItem in cartItem.Pack.Items)
                        {
                            if (packItem.Product is null || packItem.Product.ProductStatus != ProductStatus.Published)
                                unavailable.Add(new
                                {
                                    cartItemId = cartItem.CartItemId,
                                    kind   = "PACK",
                                    name   = cartItem.Pack.ProductPackName,
                                    status = packItem.Product is null
                                        ? "UNKNOWN"
                                        : EnumMappings.ProductStatusMapping.ToWire(packItem.Product.ProductStatus)
                                });
                        }
                    }
                }
            }

            if (unavailable.Count > 0)
                throw new ConflictException(
                    "Cart contains unavailable items.",
                    new { unavailable });

            // Build flat list of order line items
            var orderItems = new List<OrderItem>();
            foreach (var cartItem in cart.Items)
            {
                if (cartItem.ProductId.HasValue && cartItem.Product is not null)
                {
                    var p = cartItem.Product;
                    orderItems.Add(new OrderItem
                    {
                        OrderItemId                     = Guid.NewGuid(),
                        ProductId                       = p.ProductId,
                        PackId                          = null,
                        ProductName                     = p.ProductName,
                        OrderItemOriginalUnitPriceEuros = p.ProductPriceEuros,
                        UnitPriceEuros                  = p.ProductPriceEuros,
                        OrderItemPackDiscountPercentApplied = null,
                        Quantity                        = cartItem.Quantity,
                        LineTotalEuros                  = Math.Round(p.ProductPriceEuros * cartItem.Quantity, 2, MidpointRounding.AwayFromZero),
                        OfferType                       = p.ProductOfferType,
                        DurationMinutes                 = p.ProductDurationMinutes,
                        Sport                           = p.ProductSport,
                    });
                }
                else if (cartItem.PackId.HasValue && cartItem.Pack is not null)
                {
                    var pack = cartItem.Pack;
                    foreach (var packItem in pack.Items.OrderBy(pi => pi.ProductId))
                    {
                        if (packItem.Product is null) continue;
                        var pp         = packItem.Product;
                        // Per-line ceiling discount — Interpretation A (locked decision)
                        var netUnit    = Math.Ceiling(
                            pp.ProductPriceEuros * (1 - pack.ProductPackDiscountPercent / 100m) * 100m) / 100m;
                        orderItems.Add(new OrderItem
                        {
                            OrderItemId                     = Guid.NewGuid(),
                            ProductId                       = pp.ProductId,
                            PackId                          = pack.ProductPackId,
                            ProductName                     = pp.ProductName,
                            OrderItemOriginalUnitPriceEuros = pp.ProductPriceEuros,
                            UnitPriceEuros                  = netUnit,
                            OrderItemPackDiscountPercentApplied = pack.ProductPackDiscountPercent,
                            Quantity                        = cartItem.Quantity,
                            LineTotalEuros                  = Math.Round(netUnit * cartItem.Quantity, 2, MidpointRounding.AwayFromZero),
                            OfferType                       = pp.ProductOfferType,
                            DurationMinutes                 = pp.ProductDurationMinutes,
                            Sport                           = pp.ProductSport,
                        });
                    }
                }
            }

            // Order total = sum of ceiled line totals (no reconciliation — Interpretation A)
            var totalEuros = orderItems.Sum(oi => oi.LineTotalEuros);

            var order = new Order
            {
                OrderId           = Guid.NewGuid(),
                MemberId          = memberId,
                CoachId           = coachId,
                Status            = OrderStatus.Pending,
                TotalEuros        = totalEuros,
                StripeSessionId   = null,
                StripeCheckoutUrl = null,
                PaidAt            = null,
                CreatedDate       = DateTime.UtcNow,
                UpdatedDate       = DateTime.UtcNow,
            };
            db.Orders.Add(order);
            foreach (var oi in orderItems)
            {
                oi.OrderId = order.OrderId;
                db.OrderItems.Add(oi);
            }
            await db.SaveChangesAsync(ct);

            var stripeRequest = new CreateStripeSessionRequest(
                OrderId:   order.OrderId,
                MemberId:  memberId,
                CoachId:   coachId,
                TotalEuros: order.TotalEuros,
                LineItems: orderItems
                    .Select(oi => new CreateStripeSessionLineItem(oi.ProductName, oi.UnitPriceEuros, oi.Quantity))
                    .ToList());
            var session = await stripe.CreateSessionAsync(stripeRequest, ct);

            order.StripeSessionId   = session.SessionId;
            order.StripeCheckoutUrl = session.CheckoutUrl;
            order.UpdatedDate       = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            db.CartItems.RemoveRange(cart.Items);
            cart.UpdatedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "Checkout completed for member {MemberId} × coach {CoachId}: order {OrderId}, total {Total}€, session {SessionId}",
                memberId, coachId, order.OrderId, order.TotalEuros, session.SessionId);

            return new CheckoutResponse(order.OrderId, session.CheckoutUrl, order.TotalEuros);
        }
        catch
        {
            throw;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Coach> EnsureMemberCoachLink(Guid memberId, Guid coachId, CancellationToken ct)
    {
        var link = await db.MemberCoaches
            .Include(mc => mc.Coach)
            .FirstOrDefaultAsync(mc => mc.MemberId == memberId && mc.CoachId == coachId, ct);
        if (link is null)
            throw new NotFoundException("Coach not found for this member.");
        return link.Coach;
    }

    private static decimal ComputeEffectivePackPrice(decimal itemsTotal, decimal discountPercent)
        => Math.Ceiling(itemsTotal * (1 - discountPercent / 100m) * 100m) / 100m;

    private CartItemResponse ToCartItemResponse(CartItem item)
    {
        string  name;
        string  kind;
        decimal unitPrice;

        if (item.ProductId.HasValue)
        {
            kind      = "PRODUCT";
            name      = item.Product?.ProductName ?? "(unavailable)";
            unitPrice = item.Product?.ProductPriceEuros ?? 0m;
        }
        else
        {
            kind = "PACK";
            name = item.Pack?.ProductPackName ?? "(unavailable)";
            var packItemsTotal = item.Pack?.Items.Sum(
                pi => (pi.Product?.ProductPriceEuros ?? 0m) * pi.ProductPackItemQuantity) ?? 0m;
            unitPrice = ComputeEffectivePackPrice(
                packItemsTotal, item.Pack?.ProductPackDiscountPercent ?? 0m);
        }

        return new CartItemResponse(
            CartItemId:    item.CartItemId,
            ProductId:     item.ProductId,
            PackId:        item.PackId,
            ItemName:      name,
            ItemKind:      kind,
            UnitPriceEuros: unitPrice,
            Quantity:      item.Quantity,
            LineTotalEuros: unitPrice * item.Quantity,
            AddedDate:     item.AddedDate);
    }

    private CartResponse ToCartResponse(Cart? cart, Coach coach, Guid memberId)
    {
        var displayName = $"{coach.CoachFirstName} {coach.CoachLastName}".Trim();

        if (cart is null)
        {
            logger.LogInformation(
                "No cart exists for member {MemberId} × coach {CoachId}; returning synthesized empty cart.",
                memberId, coach.CoachId);
            return new CartResponse(
                CartId:          Guid.Empty,
                CoachId:         coach.CoachId,
                CoachDisplayName: displayName,
                Items:           [],
                TotalEuros:      0m,
                CreatedDate:     null,
                UpdatedDate:     null);
        }

        var items = cart.Items.Select(ToCartItemResponse).ToList();
        return new CartResponse(
            CartId:          cart.CartId,
            CoachId:         coach.CoachId,
            CoachDisplayName: displayName,
            Items:           items,
            TotalEuros:      items.Sum(i => i.LineTotalEuros),
            CreatedDate:     cart.CreatedDate,
            UpdatedDate:     cart.UpdatedDate);
    }

    private Task<Cart?> LoadCartWithItems(Guid memberId, Guid coachId, CancellationToken ct)
        => db.Carts
            .AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .Include(c => c.Items).ThenInclude(i => i.Pack)
                .ThenInclude(p => p!.Items).ThenInclude(pi => pi.Product)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.CoachId == coachId, ct);
}
