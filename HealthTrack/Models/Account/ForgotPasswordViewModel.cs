using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Account;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
