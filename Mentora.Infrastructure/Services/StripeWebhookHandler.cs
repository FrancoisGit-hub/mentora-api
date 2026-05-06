using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services;

public class StripeWebhookHandler(
    MentoraDbContext db,
    ILogger<StripeWebhookHandler> logger) : IStripeWebhookHandler
{
    public async Task<StripeWebhookResult> HandleAsync(
        string eventId, string eventType, string rawPayload,
        string? sessionId, CancellationToken ct)
    {
        // Step 1 — Idempotency: skip if we've already seen this event
        var existing = await db.StripeWebhookEvents
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

        if (existing is not null)
        {
            logger.LogInformation(
                "Stripe webhook event {EventId} already received (status={Status}); skipping.",
                eventId, EnumMappings.StripeEventStatusMapping.ToWire(existing.Status));
            return new StripeWebhookResult(
                Idempotent: true,
                Status: EnumMappings.StripeEventStatusMapping.ToWire(existing.Status));
        }

        var record = new StripeWebhookEvent
        {
            EventId         = eventId,
            EventType       = eventType,
            Payload         = rawPayload,
            ReceivedAt      = DateTime.UtcNow,
            Status          = StripeEventStatus.Received,
            ProcessedAt     = null,
            ProcessingError = null,
        };
        db.StripeWebhookEvents.Add(record);
        await db.SaveChangesAsync(ct);

        // Step 2 — Dispatch on event type
        try
        {
            switch (eventType)
            {
                case "checkout.session.completed":
                    await HandleCheckoutCompletedAsync(record, sessionId, ct);
                    break;
                case "checkout.session.expired":
                    await HandleCheckoutExpiredAsync(record, sessionId, ct);
                    break;
                case "payment_intent.payment_failed":
                    await HandlePaymentFailedAsync(record, sessionId, ct);
                    break;
                default:
                    logger.LogInformation(
                        "Stripe webhook event {EventId} type {EventType} not handled; marked PROCESSED.",
                        eventId, eventType);
                    record.Status     = StripeEventStatus.Processed;
                    record.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return new StripeWebhookResult(false, "PROCESSED");
            }

            record.Status     = StripeEventStatus.Processed;
            record.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return new StripeWebhookResult(false, "PROCESSED");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Stripe webhook event {EventId} type {EventType} failed during processing.",
                eventId, eventType);

            record.Status = StripeEventStatus.Failed;
            record.ProcessingError = ex.Message +
                (ex.InnerException is not null ? " | inner: " + ex.InnerException.Message : "");
            record.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return new StripeWebhookResult(false, "FAILED");
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    private async Task HandleCheckoutCompletedAsync(
        StripeWebhookEvent record, string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new InvalidOperationException("checkout.session.completed without session_id.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StripeSessionId == sessionId, ct);

        if (order is null)
        {
            logger.LogWarning(
                "checkout.session.completed for unknown sessionId {SessionId}; ignoring.",
                sessionId);
            await transaction.CommitAsync(ct);
            return;
        }

        if (order.Status == OrderStatus.Paid)
        {
            logger.LogInformation(
                "Order {OrderId} already PAID; skipping voucher regeneration.",
                order.OrderId);
            await transaction.CommitAsync(ct);
            return;
        }

        if (order.Status != OrderStatus.Pending)
        {
            logger.LogWarning(
                "Order {OrderId} is in status {Status}; cannot transition to PAID. Ignoring.",
                order.OrderId, EnumMappings.OrderStatusMapping.ToWire(order.Status));
            await transaction.CommitAsync(ct);
            return;
        }

        order.Status    = OrderStatus.Paid;
        order.PaidAt    = DateTime.UtcNow;
        order.UpdatedDate = DateTime.UtcNow;

        // Generate 1 voucher per OrderItem × Quantity
        foreach (var item in order.Items)
        {
            for (var n = 0; n < item.Quantity; n++)
            {
                db.SessionVouchers.Add(new SessionVoucher
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
                    CreatedDate       = DateTime.UtcNow,
                    UpdatedDate       = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var voucherCount = order.Items.Sum(i => i.Quantity);
        logger.LogInformation(
            "Order {OrderId} transitioned to PAID; {VoucherCount} vouchers generated for member {MemberId}.",
            order.OrderId, voucherCount, order.MemberId);
    }

    private async Task HandleCheckoutExpiredAsync(
        StripeWebhookEvent record, string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("checkout.session.expired without session_id.");
            return;
        }

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.StripeSessionId == sessionId, ct);

        if (order is null)
        {
            logger.LogWarning(
                "checkout.session.expired for unknown sessionId {SessionId}; ignoring.",
                sessionId);
            return;
        }

        if (order.Status != OrderStatus.Pending)
        {
            logger.LogInformation(
                "Order {OrderId} is already in status {Status}; skipping EXPIRED transition.",
                order.OrderId, EnumMappings.OrderStatusMapping.ToWire(order.Status));
            return;
        }

        order.Status     = OrderStatus.Expired;
        order.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} transitioned to EXPIRED.", order.OrderId);
    }

    private async Task HandlePaymentFailedAsync(
        StripeWebhookEvent record, string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("payment_intent.payment_failed without session_id.");
            return;
        }

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.StripeSessionId == sessionId, ct);

        if (order is null)
        {
            logger.LogWarning(
                "payment_intent.payment_failed for unknown sessionId {SessionId}; ignoring.",
                sessionId);
            return;
        }

        if (order.Status != OrderStatus.Pending)
        {
            logger.LogInformation(
                "Order {OrderId} is already in status {Status}; skipping FAILED transition.",
                order.OrderId, EnumMappings.OrderStatusMapping.ToWire(order.Status));
            return;
        }

        order.Status     = OrderStatus.Failed;
        order.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} transitioned to FAILED.", order.OrderId);
    }
}
