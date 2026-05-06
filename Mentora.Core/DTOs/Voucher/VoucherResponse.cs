namespace Mentora.Core.DTOs.Voucher;

public record VoucherResponse(
    Guid VoucherId,
    Guid OrderItemId,
    Guid CoachId,
    string CoachDisplayName,
    Guid ProductId,
    string ProductName,
    string OfferType,
    int DurationMinutes,
    string Sport,
    string Status,
    Guid? ReservedSessionId,
    DateTime CreatedDate,
    DateTime UpdatedDate);
