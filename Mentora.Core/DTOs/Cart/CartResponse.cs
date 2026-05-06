namespace Mentora.Core.DTOs.Cart;

public record CartResponse(
    Guid CartId,
    Guid CoachId,
    string CoachDisplayName,
    IReadOnlyList<CartItemResponse> Items,
    decimal TotalEuros,
    DateTime? CreatedDate,
    DateTime? UpdatedDate);
