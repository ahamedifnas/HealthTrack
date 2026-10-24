using HealthTrack.Models.Memberships;

namespace HealthTrack.Services.Memberships;

public interface IMembershipSubscriptionService
{
    Task<ClientSubscription?> GetActiveSubscriptionAsync(string clientId);
    Task<ClientMembershipPortalViewModel> GetMembershipSummaryAsync(string clientId);
    Task<int> ProcessExpirationsAndAlertsAsync();
    Task<(bool Success, string Message)> ExtendSubscriptionAsync(int subscriptionId, int additionalDays, string reason, string adminId);
    Task<(bool Success, string Message)> CancelSubscriptionAsync(int subscriptionId, string reason, string adminId);
}
