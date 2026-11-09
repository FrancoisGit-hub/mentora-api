namespace Mentora.Core.DTOs.Admin.Auth;

/// <summary>Lot 12.1 — exchanges an admin login code for a token pair.</summary>
public record AdminVerifyCodeRequest(string Email, string Code);
