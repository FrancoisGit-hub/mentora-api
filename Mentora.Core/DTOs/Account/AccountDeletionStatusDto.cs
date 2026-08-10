namespace Mentora.Core.DTOs.Account;

/// <param name="IsPending">Whether the authenticated user currently has a pending deletion request.</param>
/// <param name="RequestedAt">Date the request was made (UTC), or null when no request is pending.</param>
/// <param name="Reason">Reason given for the request, or null when no request is pending.</param>
public record AccountDeletionStatusDto(bool IsPending, DateTime? RequestedAt, string? Reason);
