namespace Mentora.Core.DTOs.Coach;

/// <summary>
/// Lot 14.1: result of POST /api/v1/coach/members. FirstName and LastName are the stored values
/// (for an existing member of another coach they are the names already on file, never the request ones).
/// </summary>
public sealed record AddMemberResponse(
    Guid MemberId,
    string Email,
    string FirstName,
    string LastName,
    bool IsNewAccount,
    bool IsPrimary);
