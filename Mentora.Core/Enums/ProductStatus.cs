namespace Mentora.Core.Enums;

/// <summary>
/// Reused for both Product and ProductPack — no separate ProductPackStatus enum.
/// Persisted as VARCHAR(20) UPPERCASE: DRAFT / PUBLISHED / ARCHIVED.
/// </summary>
public enum ProductStatus
{
    Draft,
    Published,
    Archived
}
