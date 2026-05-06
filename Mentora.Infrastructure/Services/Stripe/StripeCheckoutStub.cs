using Mentora.Core.DTOs.Stripe;
using Mentora.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mentora.Infrastructure.Services.Stripe;

public class StripeCheckoutStub(
    IHttpContextAccessor httpContextAccessor,
    ILogger<StripeCheckoutStub> logger) : IStripeCheckoutService
{
    public Task<StripeCheckoutSession> CreateSessionAsync(
        CreateStripeSessionRequest request,
        CancellationToken ct)
    {
        var sessionId = "cs_test_" + Guid.NewGuid().ToString("N");

        var req = httpContextAccessor.HttpContext?.Request;
        var baseUrl = req is not null
            ? $"{req.Scheme}://{req.Host}"
            : "http://localhost:5243";
        var checkoutUrl = $"{baseUrl}/api/v1/internal/stripe/fake-checkout/{sessionId}";

        logger.LogInformation(
            "Created fake Stripe session {SessionId} for order {OrderId}, total {TotalEuros}€",
            sessionId, request.OrderId, request.TotalEuros);

        return Task.FromResult(new StripeCheckoutSession(sessionId, checkoutUrl));
    }
}
