namespace Mentora.API.Authorization;

/// <summary>Lot 12.1 — names shared by Program.cs, the admin controllers and the Swagger setup.</summary>
public static class AdminAuthDefaults
{
    /// <summary>JWT bearer scheme for admin tokens (AdminAuth key, issuer and audience). Never the default scheme.</summary>
    public const string Scheme = "AdminBearer";

    /// <summary>Policy: AdminBearer scheme only, authenticated user, claim adminId.</summary>
    public const string Policy = "AdminOnly";

    /// <summary>Rate limit policy applied to every /api/v1/admin/auth route.</summary>
    public const string RateLimitPolicy = "AdminAuth";

    /// <summary>Claim carrying the admin user id.</summary>
    public const string AdminIdClaim = "adminId";

    /// <summary>Swagger document and ApiExplorer group of the admin endpoints.</summary>
    public const string SwaggerDoc = "admin";
}
