using System.Text.Json.Serialization;

namespace Mentora.Core.DTOs.Member;

/// <summary>An available session slot as seen from the member's perspective.</summary>
/// <param name="SlotId">Unique identifier of the slot.</param>
/// <param name="CoachId">Identifier of the coach who owns this slot.</param>
/// <param name="StartDate">Slot start date/time (UTC).</param>
/// <param name="EndDate">Slot end date/time (UTC).</param>
/// <param name="OfferType">Session format: VISIO, PRESENTIEL_SOLO, or PRESENTIEL_GROUPE (wire UPPERCASE).</param>
/// <param name="DurationMinutes">Slot duration in minutes.</param>
/// <param name="ProductLocation">
/// Effective location for in-person sessions: this member's per-coach address override
/// (MEMBER_COACHES.MEMBER_COACH_PRESENTIAL_ADDRESS) if set, otherwise the location of the
/// earliest PUBLISHED product on this coach that matches the slot's offer type and duration.
/// <c>null</c> when neither is set.
/// </param>
/// <param name="CompatibleWithVoucherId">
/// <c>true</c> when the slot matches the voucher supplied as the <c>voucherId</c> query parameter.
/// This field is <b>omitted entirely</b> from the JSON response when no <c>voucherId</c> was supplied.
/// </param>
/// <param name="ProductId">
/// Id of the first PUBLISHED product (by <c>PRODUCT_CREATED_DATE ASC</c>) of the slot's coach
/// that matches the slot's <c>(offerType, durationMinutes)</c>. <c>null</c> when no match exists.
/// </param>
/// <param name="ProductName">Display name of the matching product. <c>null</c> when no match exists.</param>
/// <param name="ProductPriceEuros">Price in euros of the matching product. <c>null</c> when no match exists.</param>
public record MemberSessionSlotDto(
    Guid SlotId,
    Guid CoachId,
    DateTime StartDate,
    DateTime EndDate,
    string OfferType,
    int DurationMinutes,
    string? ProductLocation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? CompatibleWithVoucherId,
    Guid? ProductId,
    string? ProductName,
    decimal? ProductPriceEuros);
