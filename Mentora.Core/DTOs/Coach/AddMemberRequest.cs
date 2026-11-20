using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Coach;

/// <summary>
/// Lot 14.1: body of POST /api/v1/coach/members. Lot 14.1b: Phone, Gender, HeightCm and BirthDate are optional
/// and stored only when the account is created.
/// </summary>
public sealed record AddMemberRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    string? Phone = null,
    Gender? Gender = null,
    short? HeightCm = null,
    DateOnly? BirthDate = null);
