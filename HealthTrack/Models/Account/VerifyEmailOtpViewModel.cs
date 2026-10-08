using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Account;

public class VerifyEmailOtpViewModel
{
    [Required(ErrorMessage = "Enter your 6-digit verification code.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Enter exactly 6 digits.")]
    [Display(Name = "Verification Code")]
    public string Code { get; set; } = string.Empty;

    public int ResendSeconds { get; set; }
    public bool DeliveryFailed { get; set; }
}
