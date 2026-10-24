using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Models.Payments;

namespace HealthTrack.Models.Memberships;

public enum SubscriptionStatus
{
    Active = 1,
    Expired = 2,
    Renewed = 3,
    Cancelled = 4
}

public class ClientSubscription
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser Client { get; set; } = null!;

    public int MembershipPackageId { get; set; }
    public MembershipPackage MembershipPackage { get; set; } = null!;

    public int PaymentTransactionId { get; set; }
    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    // Immutable snapshots at purchase time:
    [Required, MaxLength(100)]
    public string PackageNameSnapshot { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaidLKR { get; set; }

    public int DurationDaysSnapshot { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RenewedAt { get; set; }

    [MaxLength(500)]
    public string? AdminNotes { get; set; }

    public DateTime? LastModifiedAt { get; set; }
}
