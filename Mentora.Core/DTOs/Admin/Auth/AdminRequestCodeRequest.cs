namespace Mentora.Core.DTOs.Admin.Auth;

/// <summary>Lot 12.1 — asks for an admin login code. Always answered with the same 200.</summary>
public record AdminRequestCodeRequest(string Email);
