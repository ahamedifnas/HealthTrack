using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Models.Memberships;

namespace HealthTrack.Models.Payments;

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Cancelled = 4,
    ChargedBack = 5
}

public class PaymentTransaction
{
    public int Id { get; set; }
    public bool IsDemo { get; set; }

    [Required, MaxLength(50)]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser Client { get; set; } = null!;

    public int MembershipPackageId { get; set; }
    public MembershipPackage MembershipPackage { get; set; } = null!;

    // Immutable snapshots at purchase time:
    [Required, MaxLength(100)]
    public string PackageNameSnapshot { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountLKR { get; set; }

    public int DurationDaysSnapshot { get; set; }

    [Required, MaxLength(10)]
    public string Currency { get; set; } = "LKR";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(100)]
    public string? PayHerePaymentId { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    public bool SignatureVerified { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    [MaxLength(4000)]
    public string? RawPayload { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ClientSubscription? Subscription { get; set; }
}
