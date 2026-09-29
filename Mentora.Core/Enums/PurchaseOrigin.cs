namespace Mentora.Core.Enums;

/// <summary>
/// Origin of a purchase order.
/// Persisted as VARCHAR(20) UPPERCASE: MEMBER_PURCHASE / COACH_GRANT.
/// </summary>
public enum PurchaseOrigin
{
    MemberPurchase,
    CoachGrant
}
