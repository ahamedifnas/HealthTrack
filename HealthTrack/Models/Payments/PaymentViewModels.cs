using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Models.Payments;

public class PayHereNotifyModel
{
    [FromForm(Name = "merchant_id")]
    public string? MerchantId { get; set; }

    [FromForm(Name = "order_id")]
    public string? OrderId { get; set; }

    [FromForm(Name = "payment_id")]
    public string? PaymentId { get; set; }

    [FromForm(Name = "payhere_amount")]
    public string? PayHereAmount { get; set; }

    [FromForm(Name = "payhere_currency")]
    public string? PayHereCurrency { get; set; }

    [FromForm(Name = "status_code")]
    public string? StatusCode { get; set; }

    [FromForm(Name = "md5sig")]
    public string? Md5Sig { get; set; }

    [FromForm(Name = "custom_1")]
    public string? Custom1 { get; set; }

    [FromForm(Name = "custom_2")]
    public string? Custom2 { get; set; }

    [FromForm(Name = "method")]
    public string? Method { get; set; }

    [FromForm(Name = "status_message")]
    public string? StatusMessage { get; set; }

    [FromForm(Name = "card_holder_name")]
    public string? CardHolderName { get; set; }

    [FromForm(Name = "card_no")]
    public string? CardNo { get; set; }

    [FromForm(Name = "card_expiry")]
    public string? CardExpiry { get; set; }
}

public class CheckoutSummaryViewModel
{
    public bool IsDemo { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceLKR { get; set; }
    public int DurationDays { get; set; }
    public List<string> Benefits { get; set; } = new();

    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;

    public bool IsRenewal { get; set; }
    public DateOnly ProjectedStartDate { get; set; }
    public DateOnly ProjectedEndDate { get; set; }

    // PayHere Form Fields
    public string PayHereCheckoutUrl { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string NotifyUrl { get; set; } = string.Empty;
    public string FormattedAmount { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public string Hash { get; set; } = string.Empty;
}

public class PaymentReceiptViewModel
{
    public bool IsDemo { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string? PayHerePaymentId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal AmountLKR { get; set; }
    public int DurationDays { get; set; }
    public string Currency { get; set; } = "LKR";
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Subscription info
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool SubscriptionActive { get; set; }
}

public class PaymentHistoryItemViewModel
{
    public bool IsDemo { get; set; }
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal AmountLKR { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class AdminPaymentMetrics
{
    public decimal TotalRevenueLKR { get; set; }
    public int TotalTransactions { get; set; }
    public int SuccessfulPayments { get; set; }
    public int PendingPayments { get; set; }
    public int FailedPayments { get; set; }
    public int ActiveSubscriptions { get; set; }
}

public class AdminPaymentListViewModel
{
    public List<PaymentTransaction> Transactions { get; set; } = new();
    public AdminPaymentMetrics Metrics { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string? Search { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class AdminSubscriptionListViewModel
{
    public List<Memberships.ClientSubscription> Subscriptions { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string? Search { get; set; }
    public int TotalSubscriptions { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredSubscriptions { get; set; }
    public int RenewedSubscriptions { get; set; }
}
