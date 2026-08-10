namespace Mentora.Core.DTOs.Lot2;

/// <param name="From">Inclusive lower bound on slot start date. Required.</param>
/// <param name="To">Inclusive upper bound on slot start date. Required.</param>
/// <param name="IsAvailable">Optional filter on slot availability.</param>
public record ListSessionSlotsRequest(DateOnly? From, DateOnly? To, bool? IsAvailable);
