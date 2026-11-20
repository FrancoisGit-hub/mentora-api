using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Coach;

/// <summary>
/// Lot 14.1: result of POST /api/v1/coach/members. FirstName and LastName are the stored values
/// (for an existing member of another coach they are the names already on file, never the request ones).
/// Lot 14.1b: Phone, Gender, HeightCm and BirthDate are the values on file (null when never set); an existing
/// member keeps the values already stored, the request ones are ignored.
/// </summary>
public sealed record AddMemberResponse(
    Guid MemberId,
    string Email,
    string FirstName,
    string LastName,
    bool IsNewAccount,
    bool IsPrimary,
    string? Phone = null,
    Gender? Gender = null,
    short? HeightCm = null,
    DateOnly? BirthDate = null);
