using System.Text.Json;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Memberships;
using HealthTrack.Services;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services.Memberships;

public class MembershipSubscriptionService(
    ApplicationDbContext database,
    AppointmentClock clock,
    NotificationService notifications) : IMembershipSubscriptionService
{
    public async Task<ClientSubscription?> GetActiveSubscriptionAsync(string clientId)
    {
        var today = DateOnly.FromDateTime(clock.Now);
        return await database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.MembershipPackage)
            .Include(s => s.PaymentTransaction)
            .Where(s => s.ClientId == clientId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();
    }

    public async Task<ClientMembershipPortalViewModel> GetMembershipSummaryAsync(string clientId)
    {
        var today = DateOnly.FromDateTime(clock.Now);

        // 1. Reconcile any newly expired subscriptions for this user
        var expiredForUser = await database.ClientSubscriptions
            .Where(s => s.ClientId == clientId && s.Status == SubscriptionStatus.Active && s.EndDate < today)
            .ToListAsync();

        if (expiredForUser.Count > 0)
        {
            foreach (var exp in expiredForUser)
            {
                exp.Status = SubscriptionStatus.Expired;
                exp.LastModifiedAt = DateTime.UtcNow;
                notifications.Membership(
                    exp.ClientId,
                    "Membership Expired",
                    $"Your membership for '{exp.PackageNameSnapshot}' expired on {exp.EndDate:dd MMMM yyyy}. Please renew your plan to continue enjoying all HealthTrack benefits.");
            }
            await database.SaveChangesAsync();
        }

        // 2. Fetch active subscription
        var activeSub = await database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.MembershipPackage)
            .Include(s => s.PaymentTransaction)
            .Where(s => s.ClientId == clientId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        // 3. Fetch past subscriptions
        var pastSubs = await database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.PaymentTransaction)
            .Where(s => s.ClientId == clientId && (activeSub == null || s.Id != activeSub.Id))
            .OrderByDescending(s => s.ActivatedAt)
            .ToListAsync();

        // 4. Fetch available active packages for renewal/upgrade
        var packages = await database.MembershipPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PriceLKR)
            .Select(p => new MembershipPackageViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PriceLKR = p.PriceLKR,
                DurationDays = p.DurationDays,
                Benefits = ParseBenefits(p.BenefitsJson),
                IsActive = p.IsActive,
                IsCurrentSubscription = activeSub != null && activeSub.MembershipPackageId == p.Id,
                CurrentSubscriptionEndDate = activeSub != null && activeSub.MembershipPackageId == p.Id ? activeSub.EndDate : null
            })
            .ToListAsync();

        // 5. Compute metrics
        int daysRemaining = 0;
        int elapsedDays = 0;
        int totalDays = activeSub?.DurationDaysSnapshot ?? 0;
        int progressPercent = 0;
        List<string> benefits = new();

        if (activeSub != null)
        {
            daysRemaining = Math.Max(0, activeSub.EndDate.DayNumber - today.DayNumber);
            totalDays = activeSub.DurationDaysSnapshot > 0 ? activeSub.DurationDaysSnapshot : (activeSub.EndDate.DayNumber - activeSub.StartDate.DayNumber + 1);
            elapsedDays = Math.Max(0, totalDays - daysRemaining);
            progressPercent = totalDays > 0 ? Math.Clamp((int)Math.Round((double)elapsedDays * 100 / totalDays), 0, 100) : 0;
            benefits = ParseBenefits(activeSub.MembershipPackage?.BenefitsJson);
        }

        return new ClientMembershipPortalViewModel
        {
            ActiveSubscription = activeSub,
            DaysRemaining = daysRemaining,
            ElapsedDays = elapsedDays,
            TotalDurationDays = totalDays,
            ProgressPercent = progressPercent,
            CurrentBenefits = benefits,
            PastSubscriptions = pastSubs,
            AvailablePackages = packages
        };
    }

    public async Task<int> ProcessExpirationsAndAlertsAsync()
    {
        var today = DateOnly.FromDateTime(clock.Now);
        int affectedCount = 0;

        // Step A: Expire subscriptions past end date
        var expiredList = await database.ClientSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate < today)
            .ToListAsync();

        foreach (var sub in expiredList)
        {
            sub.Status = SubscriptionStatus.Expired;
            sub.LastModifiedAt = DateTime.UtcNow;
            notifications.Membership(
                sub.ClientId,
                "Membership Expired",
                $"Your membership for '{sub.PackageNameSnapshot}' expired on {sub.EndDate:dd MMMM yyyy}. Please renew your plan to continue accessing trainer appointments and exclusive features.");
            affectedCount++;
        }

        // Step B: Send 3-day and 1-day expiry warnings
        var expiringSoonList = await database.ClientSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate >= today && s.EndDate <= today.AddDays(3))
            .ToListAsync();

        var todayStartUtc = clock.Now.Date.ToUniversalTime();

        foreach (var sub in expiringSoonList)
        {
            var alreadyAlertedToday = await database.Notifications.AnyAsync(n =>
                n.UserId == sub.ClientId &&
                n.Type == NotificationType.Membership &&
                n.Title.Contains("Expiring Soon") &&
                n.CreatedAt >= todayStartUtc);

            if (!alreadyAlertedToday)
            {
                var daysLeft = sub.EndDate.DayNumber - today.DayNumber;
                var daysLabel = daysLeft == 0 ? "today" : $"in {daysLeft} day{(daysLeft == 1 ? "" : "s")}";
                notifications.Membership(
                    sub.ClientId,
                    $"Membership Expiring Soon ({daysLeft} days left)",
                    $"Your '{sub.PackageNameSnapshot}' membership is expiring {daysLabel} on {sub.EndDate:dd MMMM yyyy}. Renew now to maintain uninterrupted access without losing any remaining days.");
                affectedCount++;
            }
        }

        if (affectedCount > 0)
        {
            await database.SaveChangesAsync();
        }

        return affectedCount;
    }

    public async Task<(bool Success, string Message)> ExtendSubscriptionAsync(int subscriptionId, int additionalDays, string reason, string adminId)
    {
        if (additionalDays <= 0)
        {
            return (false, "Additional days must be greater than zero.");
        }

        var sub = await database.ClientSubscriptions
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub == null)
        {
            return (false, "Subscription record was not found.");
        }

        var today = DateOnly.FromDateTime(clock.Now);
        DateOnly newEndDate;

        if (sub.Status == SubscriptionStatus.Expired || sub.EndDate < today)
        {
            newEndDate = today.AddDays(additionalDays);
        }
        else
        {
            newEndDate = sub.EndDate.AddDays(additionalDays);
        }

        sub.EndDate = newEndDate;
        sub.Status = SubscriptionStatus.Active;
        sub.AdminNotes = $"[Extended by Admin on {clock.Now:yyyy-MM-dd HH:mm}]: +{additionalDays} days. Reason: {reason}";
        sub.LastModifiedAt = DateTime.UtcNow;

        notifications.Membership(
            sub.ClientId,
            "Membership Extended by Admin",
            $"Your '{sub.PackageNameSnapshot}' membership has been granted an administrative extension through {newEndDate:dd MMMM yyyy} (+{additionalDays} days). Note: {reason}");

        await database.SaveChangesAsync();
        return (true, $"Subscription for {sub.Client.FullName} was extended by {additionalDays} days through {newEndDate:dd MMMM yyyy}.");
    }

    public async Task<(bool Success, string Message)> CancelSubscriptionAsync(int subscriptionId, string reason, string adminId)
    {
        var sub = await database.ClientSubscriptions
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub == null)
        {
            return (false, "Subscription record was not found.");
        }

        sub.Status = SubscriptionStatus.Cancelled;
        sub.AdminNotes = $"[Cancelled by Admin on {clock.Now:yyyy-MM-dd HH:mm}]: {reason}";
        sub.LastModifiedAt = DateTime.UtcNow;

        notifications.Membership(
            sub.ClientId,
            "Membership Subscription Cancelled",
            $"Your '{sub.PackageNameSnapshot}' membership subscription was cancelled by gym administration. Note: {reason}");

        await database.SaveChangesAsync();
        return (true, $"Subscription for {sub.Client.FullName} has been cancelled.");
    }

    private static List<string> ParseBenefits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
