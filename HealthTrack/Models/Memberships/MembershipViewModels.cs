using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Memberships;

public class MembershipPackageForm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Package name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Package name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Description must be between 5 and 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price in LKR is required.")]
    [Range(0.01, 1000000.00, ErrorMessage = "Price must be between LKR 0.01 and 1,000,000.00.")]
    [Display(Name = "Price (LKR)")]
    public decimal PriceLKR { get; set; }

    [Required(ErrorMessage = "Duration in days is required.")]
    [Range(1, 3650, ErrorMessage = "Duration must be between 1 and 3650 days.")]
    [Display(Name = "Duration (Days)")]
    public int DurationDays { get; set; }

    [Display(Name = "Benefits (one per line)")]
    public string? BenefitsText { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class MembershipPackageViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceLKR { get; set; }
    public int DurationDays { get; set; }
    public List<string> Benefits { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsCurrentSubscription { get; set; }
    public DateOnly? CurrentSubscriptionEndDate { get; set; }
}

public class MembershipCatalogViewModel
{
    public List<MembershipPackageViewModel> Packages { get; set; } = new();
    public ClientSubscription? ActiveSubscription { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool IsClient { get; set; }
    public bool IsAdmin { get; set; }
}

public class ClientMembershipPortalViewModel
{
    public ClientSubscription? ActiveSubscription { get; set; }
    public int DaysRemaining { get; set; }
    public int ElapsedDays { get; set; }
    public int TotalDurationDays { get; set; }
    public int ProgressPercent { get; set; }
    public bool IsExpiringSoon => DaysRemaining is >= 0 and <= 3;
    public bool HasActiveSubscription => ActiveSubscription != null;
    public List<string> CurrentBenefits { get; set; } = new();
    public List<ClientSubscription> PastSubscriptions { get; set; } = new();
    public List<MembershipPackageViewModel> AvailablePackages { get; set; } = new();
}

public class AdminSubscriptionManageViewModel
{
    public int SubscriptionId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal AmountPaidLKR { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? LinkedOrderId { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime ActivatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    [Range(1, 365, ErrorMessage = "Extension must be between 1 and 365 days.")]
    public int AdditionalDays { get; set; } = 14;

    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Reason must be between 3 and 200 characters.")]
    public string Reason { get; set; } = string.Empty;
}

