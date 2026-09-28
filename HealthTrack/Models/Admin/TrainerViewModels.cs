using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Admin;

public class CreateTrainerViewModel
{
    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public record TrainerListItem(string Id, string FullName, string? Email, bool IsActive, DateTime CreatedAt)
{
    public string StatusLabel => IsActive ? "Active" : "Inactive";
    public string StatusCss => IsActive ? "bg-success" : "bg-secondary";
    public string NextActiveValue => (!IsActive).ToString().ToLowerInvariant();
    public string StatusAction => IsActive ? "Deactivate" : "Activate";
    public string StatusActionCss => IsActive ? "btn-outline-danger" : "btn-outline-success";
}
