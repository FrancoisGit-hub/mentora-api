namespace Mentora.Core.DTOs.Voucher;

/// <summary>Result of a coach granting free session vouchers to a member.</summary>
/// <param name="PurchaseId">Identifier of the zero-amount purchase (order) created for this grant.</param>
/// <param name="MemberId">Member the vouchers were granted to.</param>
/// <param name="ProductId">Product the vouchers were granted for.</param>
/// <param name="Quantity">Number of vouchers requested.</param>
/// <param name="VoucherCount">Number of vouchers actually created (equal to <paramref name="Quantity"/>).</param>
/// <param name="VoucherIds">Identifiers of the created vouchers.</param>
/// <param name="Origin">Always <c>COACH_GRANT</c>.</param>
/// <param name="GrantedDate">Timestamp the grant was recorded (UTC).</param>
public record VoucherGrantResponse(
    Guid PurchaseId,
    Guid MemberId,
    Guid ProductId,
    int Quantity,
    int VoucherCount,
    IReadOnlyList<Guid> VoucherIds,
    string Origin,
    DateTime GrantedDate
);
