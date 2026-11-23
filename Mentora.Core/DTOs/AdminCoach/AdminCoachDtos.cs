namespace Mentora.Core.DTOs.AdminCoach;

// Lot 14.8a: admin back-office, creation and list of coach accounts.

/// <summary>Body of POST /api/v1/admin/coaches. Strings are trimmed; a blank phone becomes null.</summary>
/// <param name="Email">Required, valid address, 255 characters or fewer once trimmed and lower-cased.</param>
/// <param name="FirstName">Required, 100 characters or fewer once trimmed.</param>
/// <param name="LastName">Required, 100 characters or fewer once trimmed.</param>
/// <param name="Phone">Optional, 8 to 20 characters: digits, spaces, +, -, ., or parentheses.</param>
public record AdminCreateCoachRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    string? Phone);

/// <summary>One coach of the admin list, also the body of the 201 of a creation (memberCount 0).</summary>
/// <param name="CoachId">COACHES.COACH_ID.</param>
/// <param name="UserId">USERS.USER_ID.</param>
/// <param name="Email">USERS.USER_EMAIL (lower case).</param>
/// <param name="FirstName">COACHES.COACH_FIRST_NAME.</param>
/// <param name="LastName">COACHES.COACH_LAST_NAME.</param>
/// <param name="Phone">COACHES.COACH_PHONE, null when absent.</param>
/// <param name="CreatedDate">COACHES.COACH_CREATED_DATE (UTC).</param>
/// <param name="IsEnabled">USERS.USER_IS_ENABLED.</param>
/// <param name="IsActive">COACHES.COACH_IS_ACTIVE.</param>
/// <param name="MemberCount">Linked members whose member profile is active.</param>
public record AdminCoachListItem(
    Guid CoachId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    DateTime CreatedDate,
    bool IsEnabled,
    bool IsActive,
    int MemberCount);

/// <summary>Page of the admin coach list: newest first, <c>NextCursor</c> null on the last page.</summary>
public record AdminCoachListResponse(
    IReadOnlyList<AdminCoachListItem> Items,
    string? NextCursor);
