namespace Mentora.Core.DTOs.Coach;

/// <summary>Lot 14.1: body of POST /api/v1/coach/members.</summary>
public sealed record AddMemberRequest(string? Email, string? FirstName, string? LastName);
