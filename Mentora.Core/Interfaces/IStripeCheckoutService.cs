using Mentora.Core.DTOs.Stripe;

namespace Mentora.Core.Interfaces;

public interface IStripeCheckoutService
{
    Task<StripeCheckoutSession> CreateSessionAsync(
        CreateStripeSessionRequest request,
        CancellationToken ct);
}
