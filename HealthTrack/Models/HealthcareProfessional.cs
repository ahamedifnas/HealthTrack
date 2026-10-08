using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// Name, email and active status have one source of truth: the Identity account.
public class HealthcareProfessional
{
    [Key] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Specialty { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
