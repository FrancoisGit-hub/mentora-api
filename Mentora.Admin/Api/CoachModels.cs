namespace Mentora.Admin.Api;

/// <summary>Une ligne de la liste des coachs (GET api/v1/admin/coaches).</summary>
public sealed record CoachListItem(
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

public sealed record CoachListPage(IReadOnlyList<CoachListItem> Items, string? NextCursor);

public sealed record CreateCoachRequest(string Email, string FirstName, string LastName, string? Phone);
