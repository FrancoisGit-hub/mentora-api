using Mentora.Core.DTOs.AdminCoach;

namespace Mentora.Core.Interfaces;

/// <summary>Lot 14.8a: admin back-office, list and creation of coach accounts.</summary>
public interface IAdminCoachService
{
    /// <summary>All coaches, newest first. Invalid limit or cursor throw InvalidOperationException (400).</summary>
    Task<AdminCoachListResponse> ListAsync(int limit, string? cursor, CancellationToken ct);

    /// <summary>
    /// Creates the account (user, coach, parameters, 4 offer programs, audit row) in one transaction.
    /// ConflictException EMAIL_TAKEN when any user already holds the email.
    /// </summary>
    Task<AdminCoachListItem> CreateAsync(AdminCreateCoachRequest request, Guid adminUserId, CancellationToken ct);
}
