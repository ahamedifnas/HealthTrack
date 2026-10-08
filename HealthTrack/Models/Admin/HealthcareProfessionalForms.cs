using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Admin;

public class CreateHealthcareProfessionalForm : CreateTrainerViewModel
{
    [Required, StringLength(100)] public string Specialty { get; set; } = string.Empty;
}

public class EditHealthcareProfessionalForm
{
    [Required, StringLength(100), Display(Name = "Full name")] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Specialty { get; set; } = string.Empty;
    [Display(Name = "Active account")] public bool IsActive { get; set; }
}
