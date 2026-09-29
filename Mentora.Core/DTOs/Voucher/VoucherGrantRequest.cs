namespace Mentora.Core.DTOs.Voucher;

/// <summary>Payload for a coach granting free session vouchers to one of their members.</summary>
/// <param name="ProductId">Unique identifier of the product the vouchers are granted for. Must belong to the authenticated coach.</param>
/// <param name="Quantity">Number of vouchers to grant. Accepted range: [1..50].</param>
/// <param name="Note">Optional free-text note explaining the grant. Max 500 characters.</param>
public record VoucherGrantRequest(
    Guid ProductId,
    int Quantity,
    string? Note
);
