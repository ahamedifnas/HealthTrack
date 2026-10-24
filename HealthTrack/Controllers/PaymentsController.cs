using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Memberships;
using HealthTrack.Models.Payments;
using HealthTrack.Services;
using HealthTrack.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using HealthTrack.Services.Memberships;

namespace HealthTrack.Controllers;

[AutoValidateAntiforgeryToken]
public class PaymentsController(
    ApplicationDbContext database,
    UserManager<ApplicationUser> users,
    IPayHerePaymentService payHereService,
    AppointmentClock clock,
    NotificationService notifications,
    IMembershipSubscriptionService membershipService,
    PaymentMode paymentMode) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Checkout(int packageId)
    {
        var package = await database.MembershipPackages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);
        if (package is null)
        {
            TempData["Error"] = "Selected package is not available.";
            return RedirectToAction("Index", "MembershipPackages");
        }

        var client = await users.FindByIdAsync(UserId);
        if (client is null) return NotFound();

        var today = DateOnly.FromDateTime(clock.Now);
        var activeSub = await database.ClientSubscriptions.AsNoTracking()
            .Where(s => s.ClientId == UserId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        var isRenewal = activeSub is not null;
        var projectedStart = isRenewal ? activeSub!.EndDate.AddDays(1) : today;
        var projectedEnd = projectedStart.AddDays(package.DurationDays - 1);

        var orderId = paymentMode.IsDemo ? $"DEMO-{Guid.NewGuid():N}" : payHereService.GenerateOrderId();
        var formattedAmount = package.PriceLKR.ToString("0.00", CultureInfo.InvariantCulture);
        var hash = paymentMode.IsDemo ? string.Empty : payHereService.GenerateCheckoutHash(orderId, package.PriceLKR, payHereService.Currency);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/Payments/Success?orderId={orderId}";
        var cancelUrl = $"{baseUrl}/Payments/Cancelled?orderId={orderId}";
        var notifyUrl = $"{baseUrl}/Payments/Notify";

        var vm = new CheckoutSummaryViewModel
        {
            IsDemo = paymentMode.IsDemo,
            OrderId = orderId,
            PackageId = package.Id,
            PackageName = package.Name,
            Description = package.Description,
            PriceLKR = package.PriceLKR,
            DurationDays = package.DurationDays,
            Benefits = ParseBenefits(package.BenefitsJson),
            ClientName = client.FullName,
            ClientEmail = client.Email ?? string.Empty,
            IsRenewal = isRenewal,
            ProjectedStartDate = projectedStart,
            ProjectedEndDate = projectedEnd,
            PayHereCheckoutUrl = payHereService.CheckoutUrl,
            MerchantId = payHereService.MerchantId,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            NotifyUrl = notifyUrl,
            FormattedAmount = formattedAmount,
            Currency = payHereService.Currency,
            Hash = hash
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Initiate(int packageId)
    {
        var package = await database.MembershipPackages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);
        if (package is null)
        {
            TempData["Error"] = "Selected package is not available.";
            return RedirectToAction("Index", "MembershipPackages");
        }

        var client = await users.FindByIdAsync(UserId);
        if (client is null) return NotFound();

        var orderId = paymentMode.IsDemo ? $"DEMO-{Guid.NewGuid():N}" : payHereService.GenerateOrderId();
        var formattedAmount = package.PriceLKR.ToString("0.00", CultureInfo.InvariantCulture);
        var hash = paymentMode.IsDemo ? string.Empty : payHereService.GenerateCheckoutHash(orderId, package.PriceLKR, payHereService.Currency);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/Payments/Success?orderId={orderId}";
        var cancelUrl = $"{baseUrl}/Payments/Cancelled?orderId={orderId}";
        var notifyUrl = $"{baseUrl}/Payments/Notify";

        // Save pending transaction with immutable snapshots
        var transaction = new PaymentTransaction
        {
            IsDemo = paymentMode.IsDemo,
            OrderId = orderId,
            ClientId = UserId,
            MembershipPackageId = package.Id,
            PackageNameSnapshot = package.Name,
            AmountLKR = package.PriceLKR,
            DurationDaysSnapshot = package.DurationDays,
            Currency = payHereService.Currency,
            Status = PaymentStatus.Pending,
            PaymentMethod = paymentMode.IsDemo ? PaymentMode.Label : null,
            CreatedAt = DateTime.UtcNow
        };

        database.PaymentTransactions.Add(transaction);
        await database.SaveChangesAsync();

        var vm = new CheckoutSummaryViewModel
        {
            IsDemo = paymentMode.IsDemo,
            OrderId = orderId,
            PackageId = package.Id,
            PackageName = package.Name,
            Description = package.Description,
            PriceLKR = package.PriceLKR,
            DurationDays = package.DurationDays,
            ClientName = client.FullName,
            ClientEmail = client.Email ?? string.Empty,
            PayHereCheckoutUrl = payHereService.CheckoutUrl,
            MerchantId = payHereService.MerchantId,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            NotifyUrl = notifyUrl,
            FormattedAmount = formattedAmount,
            Currency = payHereService.Currency,
            Hash = hash
        };

        return paymentMode.IsDemo ? RedirectToAction(nameof(Demo), new { orderId }) : View("PayHereRedirect", vm);
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("Payments/Notify")]
    public async Task<IActionResult> Notify([FromForm] PayHereNotifyModel model)
    {
        if (string.IsNullOrWhiteSpace(model.OrderId) ||
            string.IsNullOrWhiteSpace(model.PayHereAmount) ||
            string.IsNullOrWhiteSpace(model.PayHereCurrency) ||
            string.IsNullOrWhiteSpace(model.StatusCode) ||
            string.IsNullOrWhiteSpace(model.Md5Sig))
        {
            return BadRequest("Missing required IPN parameters.");
        }

        // 1. Verify MD5 signature against merchant secret
        var isSignatureValid = payHereService.VerifyIpnSignature(
            model.OrderId,
            model.PayHereAmount,
            model.PayHereCurrency,
            model.StatusCode,
            model.Md5Sig);

        var transaction = await database.PaymentTransactions
            .Include(t => t.MembershipPackage)
            .FirstOrDefaultAsync(t => t.OrderId == model.OrderId && !t.IsDemo);

        if (transaction is null)
        {
            return NotFound("Order not found.");
        }

        if (!isSignatureValid)
        {
            transaction.SignatureVerified = false;
            transaction.FailureReason = "Invalid PayHere MD5 signature detected.";
            transaction.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync();
            return BadRequest("Signature verification failed.");
        }

        transaction.SignatureVerified = true;
        transaction.PayHerePaymentId = model.PaymentId;
        transaction.PaymentMethod = model.Method;
        transaction.RawPayload = $"StatusCode={model.StatusCode};PaymentId={model.PaymentId};Method={model.Method};Msg={model.StatusMessage}";
        transaction.UpdatedAt = DateTime.UtcNow;

        // 2. Verify merchant ID and currency
        if (!string.Equals(model.MerchantId, payHereService.MerchantId, StringComparison.OrdinalIgnoreCase))
        {
            transaction.FailureReason = "Merchant ID mismatch.";
            await database.SaveChangesAsync();
            return BadRequest("Merchant ID mismatch.");
        }

        if (!string.Equals(model.PayHereCurrency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            transaction.FailureReason = "Currency mismatch.";
            await database.SaveChangesAsync();
            return BadRequest("Currency mismatch.");
        }

        // 3. Verify amount matches internal purchase price
        if (decimal.TryParse(model.PayHereAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var paidAmount))
        {
            if (paidAmount != transaction.AmountLKR)
            {
                transaction.FailureReason = $"Amount mismatch. Expected {transaction.AmountLKR}, received {paidAmount}.";
                await database.SaveChangesAsync();
                return BadRequest("Amount mismatch.");
            }
        }
        else
        {
            transaction.FailureReason = "Invalid amount format.";
            await database.SaveChangesAsync();
            return BadRequest("Invalid amount format.");
        }

        // 4. Idempotency check: if already paid, acknowledge without double-processing
        if (transaction.Status == PaymentStatus.Paid)
        {
            return Ok("Already processed.");
        }

        // 5. Status Code 2 means success in PayHere
        if (model.StatusCode == "2")
        {
            transaction.Status = PaymentStatus.Paid;
            transaction.PaidAt = DateTime.UtcNow;

            // Transactional subscription activation / renewal
            var today = DateOnly.FromDateTime(clock.Now);
            var activeSub = await database.ClientSubscriptions
                .Where(s => s.ClientId == transaction.ClientId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            DateOnly startDate, endDate;
            if (activeSub != null)
            {
                startDate = activeSub.EndDate.AddDays(1);
                endDate = startDate.AddDays(transaction.DurationDaysSnapshot - 1);
                activeSub.Status = SubscriptionStatus.Renewed;
                activeSub.RenewedAt = DateTime.UtcNow;
            }
            else
            {
                startDate = today;
                endDate = today.AddDays(transaction.DurationDaysSnapshot - 1);
            }

            var subscription = new ClientSubscription
            {
                ClientId = transaction.ClientId,
                MembershipPackageId = transaction.MembershipPackageId,
                PaymentTransactionId = transaction.Id,
                PackageNameSnapshot = transaction.PackageNameSnapshot,
                AmountPaidLKR = transaction.AmountLKR,
                DurationDaysSnapshot = transaction.DurationDaysSnapshot,
                StartDate = startDate,
                EndDate = endDate,
                Status = SubscriptionStatus.Active,
                ActivatedAt = DateTime.UtcNow
            };

            database.ClientSubscriptions.Add(subscription);

            // Add in-app notification
            notifications.Membership(
                transaction.ClientId,
                "Payment Successful & Subscription Activated",
                $"Your payment of LKR {transaction.AmountLKR:N2} for '{transaction.PackageNameSnapshot}' has been verified. Your membership is active through {endDate:dd MMMM yyyy}.");
        }
        else if (model.StatusCode == "-1")
        {
            transaction.Status = PaymentStatus.Cancelled;
            transaction.FailureReason = model.StatusMessage ?? "Payment cancelled by customer.";
        }
        else if (model.StatusCode == "-2")
        {
            transaction.Status = PaymentStatus.Failed;
            transaction.FailureReason = model.StatusMessage ?? "Payment transaction failed at gateway.";
        }
        else if (model.StatusCode == "-3")
        {
            transaction.Status = PaymentStatus.ChargedBack;
            transaction.FailureReason = model.StatusMessage ?? "Chargeback reported.";
        }

        await database.SaveChangesAsync();
        return Ok("OK");
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Success(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return RedirectToAction(nameof(History));

        var transaction = await database.PaymentTransactions.AsNoTracking()
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.OrderId == orderId && t.ClientId == UserId);

        if (transaction is null) return NotFound();

        var client = await users.FindByIdAsync(UserId);

        var vm = new PaymentReceiptViewModel
        {
            IsDemo = transaction.IsDemo,
            OrderId = transaction.OrderId,
            PayHerePaymentId = transaction.PayHerePaymentId,
            ClientName = client?.FullName ?? "Member",
            ClientEmail = client?.Email ?? string.Empty,
            PackageName = transaction.PackageNameSnapshot,
            AmountLKR = transaction.AmountLKR,
            DurationDays = transaction.DurationDaysSnapshot,
            Currency = transaction.Currency,
            Status = transaction.Status,
            PaymentMethod = transaction.PaymentMethod,
            CreatedAt = transaction.CreatedAt,
            PaidAt = transaction.PaidAt,
            StartDate = transaction.Subscription?.StartDate,
            EndDate = transaction.Subscription?.EndDate,
            SubscriptionActive = transaction.Subscription?.Status == SubscriptionStatus.Active
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Cancelled(string? orderId)
    {
        ViewBag.OrderId = orderId;
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var transaction = await database.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId && t.ClientId == UserId && t.Status == PaymentStatus.Pending && !t.IsDemo);
            if (transaction != null)
            {
                transaction.Status = PaymentStatus.Cancelled;
                transaction.FailureReason = "Customer cancelled during gateway checkout.";
                transaction.UpdatedAt = DateTime.UtcNow;
                await database.SaveChangesAsync();
            }
        }
        return View();
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> History()
    {
        var transactions = await database.PaymentTransactions.AsNoTracking()
            .Where(t => t.ClientId == UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PaymentHistoryItemViewModel
            {
                Id = t.Id,
                IsDemo = t.IsDemo,
                OrderId = t.OrderId,
                PackageName = t.PackageNameSnapshot,
                AmountLKR = t.AmountLKR,
                Status = t.Status,
                PaymentMethod = t.PaymentMethod,
                CreatedAt = t.CreatedAt,
                PaidAt = t.PaidAt
            })
            .ToListAsync();

        return View(transactions);
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Membership()
    {
        var vm = await membershipService.GetMembershipSummaryAsync(UserId);
        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Renew(int? packageId)
    {
        if (packageId.HasValue && packageId.Value > 0)
        {
            return RedirectToAction(nameof(Checkout), new { packageId = packageId.Value });
        }

        var activeSub = await membershipService.GetActiveSubscriptionAsync(UserId);
        if (activeSub != null)
        {
            return RedirectToAction(nameof(Checkout), new { packageId = activeSub.MembershipPackageId });
        }

        return RedirectToAction("Index", "MembershipPackages");
    }

    [HttpGet]
    [Route("Payments/Receipt/{orderId?}")]
    [Authorize]
    public async Task<IActionResult> Receipt(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            orderId = Request.Query["orderId"].FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(orderId)) return RedirectToAction(nameof(History));

        var query = database.PaymentTransactions.AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.Subscription)
            .Where(t => t.OrderId == orderId);

        if (!User.IsInRole(AppRoles.Admin))
        {
            query = query.Where(t => t.ClientId == UserId);
        }

        var transaction = await query.FirstOrDefaultAsync();
        if (transaction is null) return NotFound();

        var vm = new PaymentReceiptViewModel
        {
            IsDemo = transaction.IsDemo,
            OrderId = transaction.OrderId,
            PayHerePaymentId = transaction.PayHerePaymentId,
            ClientName = transaction.Client.FullName,
            ClientEmail = transaction.Client.Email ?? string.Empty,
            PackageName = transaction.PackageNameSnapshot,
            AmountLKR = transaction.AmountLKR,
            DurationDays = transaction.DurationDaysSnapshot,
            Currency = transaction.Currency,
            Status = transaction.Status,
            PaymentMethod = transaction.PaymentMethod,
            CreatedAt = transaction.CreatedAt,
            PaidAt = transaction.PaidAt,
            StartDate = transaction.Subscription?.StartDate,
            EndDate = transaction.Subscription?.EndDate,
            SubscriptionActive = transaction.Subscription?.Status == SubscriptionStatus.Active
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Demo(string orderId)
    {
        if (!paymentMode.IsDemo || !PaymentMode.IsLocalRequest(HttpContext)) return NotFound();
        var transaction = await database.PaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.OrderId == orderId && t.ClientId == UserId && t.IsDemo);
        return transaction is null ? NotFound() : View(transaction);
    }

    [HttpPost]
    [Authorize(Policy = FeaturePolicies.Client)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDemo(string orderId, string outcome)
    {
        if (!paymentMode.IsDemo || !PaymentMode.IsLocalRequest(HttpContext)) return NotFound();
        if (outcome is not ("success" or "failed" or "cancelled")) return BadRequest();
        // Serialize a client's purchases, including separate pending orders and repeated submissions.
        await using var scope = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM AspNetUsers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {UserId}");
        var transaction = await database.PaymentTransactions
            .FirstOrDefaultAsync(t => t.OrderId == orderId && t.ClientId == UserId && t.IsDemo);
        if (transaction is null) return NotFound();
        if (transaction.Status != PaymentStatus.Pending)
            return RedirectToAction(nameof(Demo), new { orderId });
        var package = await database.MembershipPackages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == transaction.MembershipPackageId && p.IsActive);
        if (package is null || package.PriceLKR != transaction.AmountLKR
            || package.DurationDays != transaction.DurationDaysSnapshot || transaction.Currency != "LKR")
            return Conflict("Package changed. Start a new checkout.");
        if (transaction.CreatedAt < DateTime.UtcNow.AddMinutes(-30))
            return Conflict("Demo checkout expired. Start a new checkout.");
        transaction.PaymentMethod = PaymentMode.Label;
        transaction.UpdatedAt = DateTime.UtcNow;
        if (outcome == "success")
        {
            transaction.Status = PaymentStatus.Paid;
            transaction.PaidAt = DateTime.UtcNow;
            // Transactional subscription activation / renewal
            var today = DateOnly.FromDateTime(clock.Now);
            var activeSub = await database.ClientSubscriptions
                .Where(s => s.ClientId == transaction.ClientId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            DateOnly startDate, endDate;
            if (activeSub != null)
            {
                startDate = activeSub.EndDate.AddDays(1);
                endDate = startDate.AddDays(transaction.DurationDaysSnapshot - 1);
                activeSub.Status = SubscriptionStatus.Renewed;
                activeSub.RenewedAt = DateTime.UtcNow;
            }
            else
            {
                startDate = today;
                endDate = today.AddDays(transaction.DurationDaysSnapshot - 1);
            }

            var subscription = new ClientSubscription
            {
                ClientId = transaction.ClientId,
                MembershipPackageId = transaction.MembershipPackageId,
                PaymentTransactionId = transaction.Id,
                PackageNameSnapshot = transaction.PackageNameSnapshot,
                AmountPaidLKR = transaction.AmountLKR,
                DurationDaysSnapshot = transaction.DurationDaysSnapshot,
                StartDate = startDate,
                EndDate = endDate,
                Status = SubscriptionStatus.Active,
                ActivatedAt = DateTime.UtcNow
            };

            database.ClientSubscriptions.Add(subscription);

            // Add in-app notification
            notifications.Membership(
                transaction.ClientId,
                "Demo Payment – No Real Money: Membership Activated",
                $"Your payment of LKR {transaction.AmountLKR:N2} for '{transaction.PackageNameSnapshot}' was simulated; no real money was collected. Your membership is active through {endDate:dd MMMM yyyy}.");
        }
        else
        {
            transaction.Status = outcome == "failed" ? PaymentStatus.Failed : PaymentStatus.Cancelled;
            transaction.FailureReason = $"Simulated {outcome}; no real money.";
        }
        await database.SaveChangesAsync();
        await scope.CommitAsync();
        return RedirectToAction(nameof(Demo), new { orderId });
    }

    private static List<string> ParseBenefits(string? benefitsJson)
    {
        if (string.IsNullOrWhiteSpace(benefitsJson)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(benefitsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
