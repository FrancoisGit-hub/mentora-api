namespace Mentora.Core.DTOs;

public record PagedResult<T>(List<T> Items, int TotalItems, int Page, int Size);
