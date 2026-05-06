namespace Mentora.Core.DTOs.Stripe;

public record StripeCheckoutSession(
    string SessionId,
    string CheckoutUrl);
