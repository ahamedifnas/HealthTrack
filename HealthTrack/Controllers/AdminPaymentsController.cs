using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Memberships;
using HealthTrack.Models.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using HealthTrack.Services;
using HealthTrack.Services.Memberships;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[AutoValidateAntiforgeryToken]
public class AdminPaymentsController(
    ApplicationDbContext database,
    IMembershipSubscriptionService membershipService,
    AppointmentClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? search, DateOnly? from, DateOnly? to)
    {
        var query = database.PaymentTransactions.AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.MembershipPackage)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => t.OrderId.Contains(s) ||
                                     t.Client.Email!.Contains(s) ||
                                     t.Client.FullName.Contains(s) ||
                                     t.PackageNameSnapshot.Contains(s));
        }

        if (from.HasValue)
        {
            var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(t => t.CreatedAt >= fromDt);
        }

        if (to.HasValue)
        {
            var toDt = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(t => t.CreatedAt <= toDt);
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Calculate system metrics
        var allTx = await database.PaymentTransactions.AsNoTracking().ToListAsync();
        var totalRevenue = allTx.Where(t => !t.IsDemo && t.Status == PaymentStatus.Paid).Sum(t => t.AmountLKR);
        var activeSubs = await database.ClientSubscriptions.AsNoTracking()
            .CountAsync(s => s.Status == SubscriptionStatus.Active);

        var metrics = new AdminPaymentMetrics
        {
            TotalRevenueLKR = totalRevenue,
            TotalTransactions = allTx.Count,
            SuccessfulPayments = allTx.Count(t => !t.IsDemo && t.Status == PaymentStatus.Paid),
            PendingPayments = allTx.Count(t => t.Status == PaymentStatus.Pending),
            FailedPayments = allTx.Count(t => t.Status == PaymentStatus.Failed || t.Status == PaymentStatus.Cancelled),
            ActiveSubscriptions = activeSubs
        };

        var vm = new AdminPaymentListViewModel
        {
            Transactions = transactions,
            Metrics = metrics,
            SelectedStatus = status,
            Search = search,
            FromDate = from,
            ToDate = to
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Subscriptions(string? status, string? search)
    {
        var query = database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.MembershipPackage)
            .Include(s => s.PaymentTransaction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubscriptionStatus>(status, out var parsedStatus))
        {
            query = query.Where(s => s.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchStr = search.Trim();
            query = query.Where(sub => sub.Client.Email!.Contains(searchStr) ||
                                       sub.Client.FullName.Contains(searchStr) ||
                                       sub.PackageNameSnapshot.Contains(searchStr));
        }

        var subscriptions = await query
            .OrderByDescending(s => s.ActivatedAt)
            .ToListAsync();

        var today = DateOnly.FromDateTime(clock.Now);
        var allSubs = await database.ClientSubscriptions.AsNoTracking().ToListAsync();

        var vm = new AdminSubscriptionListViewModel
        {
            Subscriptions = subscriptions,
            SelectedStatus = status,
            Search = search,
            TotalSubscriptions = allSubs.Count,
            ActiveSubscriptions = allSubs.Count(s => s.Status == SubscriptionStatus.Active && s.EndDate >= today),
            ExpiringSoonCount = allSubs.Count(s => s.Status == SubscriptionStatus.Active && s.EndDate >= today && s.EndDate <= today.AddDays(3)),
            ExpiredSubscriptions = allSubs.Count(s => s.Status == SubscriptionStatus.Expired || (s.Status == SubscriptionStatus.Active && s.EndDate < today)),
            RenewedSubscriptions = allSubs.Count(s => s.Status == SubscriptionStatus.Renewed)
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ManageSubscription(int id)
    {
        var sub = await database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.PaymentTransaction)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sub == null) return NotFound();

        var vm = new AdminSubscriptionManageViewModel
        {
            SubscriptionId = sub.Id,
            ClientId = sub.ClientId,
            ClientName = sub.Client.FullName,
            ClientEmail = sub.Client.Email ?? string.Empty,
            PackageName = sub.PackageNameSnapshot,
            AmountPaidLKR = sub.AmountPaidLKR,
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            Status = sub.Status,
            LinkedOrderId = sub.PaymentTransaction?.OrderId,
            AdminNotes = sub.AdminNotes,
            ActivatedAt = sub.ActivatedAt,
            LastModifiedAt = sub.LastModifiedAt,
            AdditionalDays = 14,
            Reason = string.Empty
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendSubscription(int subscriptionId, int additionalDays, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "A valid reason for extension must be provided.";
            return RedirectToAction(nameof(ManageSubscription), new { id = subscriptionId });
        }

        var result = await membershipService.ExtendSubscriptionAsync(subscriptionId, additionalDays, reason.Trim(), User.Identity?.Name ?? "Admin");
        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription(int subscriptionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "A valid reason for cancellation must be provided.";
            return RedirectToAction(nameof(ManageSubscription), new { id = subscriptionId });
        }

        var result = await membershipService.CancelSubscriptionAsync(subscriptionId, reason.Trim(), User.Identity?.Name ?? "Admin");
        if (result.Success)
        {
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunExpirationCheck()
    {
        var count = await membershipService.ProcessExpirationsAndAlertsAsync();
        TempData["Success"] = $"Reconciliation check complete. {count} subscription(s) updated or alerted.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var transaction = await database.PaymentTransactions.AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.MembershipPackage)
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction is null) return NotFound();

        return View(transaction);
    }
}
