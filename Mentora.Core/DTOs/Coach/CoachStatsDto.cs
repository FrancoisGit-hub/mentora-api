namespace Mentora.Core.DTOs.Coach;

/// <summary>Home-screen activity and revenue stats for the authenticated coach.</summary>
/// <param name="AppointmentsThisMonth">Count of Scheduled or Completed sessions with SessionScheduledAt in the current month.</param>
/// <param name="RevenueThisMonth">Sum of TotalEuros for Paid orders with PaidAt in the current month.</param>
/// <param name="SessionsSoldThisMonth">Count of session vouchers created in the current month.</param>
/// <param name="RevenuePrevMonth">Sum of TotalEuros for Paid orders with PaidAt in the previous month.</param>
/// <param name="SessionsSoldPrevMonth">Count of session vouchers created in the previous month.</param>
/// <param name="ActiveMembersCount">Count of active members currently linked to this coach.</param>
/// <param name="RevenueTotal">Sum of TotalEuros for all Paid orders, all-time.</param>
public record CoachStatsDto(
    int AppointmentsThisMonth,
    decimal RevenueThisMonth,
    int SessionsSoldThisMonth,
    decimal RevenuePrevMonth,
    int SessionsSoldPrevMonth,
    int ActiveMembersCount,
    decimal RevenueTotal);
