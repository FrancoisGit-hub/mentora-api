namespace Mentora.Core.DTOs.Coach;

/// <summary>Full replace. All three fields are required (null or missing = 422).</summary>
public record UpdateSessionPreferencesRequest(
    bool? OffersVisio,
    bool? OffersInPersonSolo,
    List<string>? InPersonLocations);

public record SessionPreferencesDto(
    bool OffersVisio,
    bool OffersInPersonSolo,
    List<string> InPersonLocations);
