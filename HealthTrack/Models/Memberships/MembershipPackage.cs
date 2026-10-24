using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Models.Payments;

namespace HealthTrack.Models.Memberships;

public class MembershipPackage
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PriceLKR { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [MaxLength(2000)]
    public string? BenefitsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClientSubscription> Subscriptions { get; set; } = new List<ClientSubscription>();
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
