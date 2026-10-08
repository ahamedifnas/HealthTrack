using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Account;

public class ResetPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
