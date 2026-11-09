namespace Mentora.Admin.Api;

// DTO clients, alignés sur le JSON de /api/v1/admin/auth (camelCase côté API).
public sealed record RequestCodeBody(string Email);

public sealed record VerifyCodeBody(string Email, string Code);

public sealed record RefreshBody(string RefreshToken);

public sealed record LogoutBody(string RefreshToken);

public sealed record AdminMe(Guid AdminUserId, string Email);

public sealed record AdminAuthData(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn,
    AdminMe Admin);

/// <summary>Corps vide : la réponse n'a pas de données.</summary>
public sealed record NoData;
