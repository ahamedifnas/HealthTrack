using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public decimal? TargetWeight { get; set; }
    public ICollection<HealthMeasurement> HealthMeasurements { get; set; } = new List<HealthMeasurement>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    // Opt-in only for new public Client registrations. Legacy and staff accounts remain unaffected.
    public bool RequiresRegistrationEmailVerification { get; set; }
}
