using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Admin;

public class EditTrainerViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string Id { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
