namespace Mentora.Core.DTOs.Session;

public record ReserveSessionRequest(
    Guid VoucherId,
    Guid SlotId);
