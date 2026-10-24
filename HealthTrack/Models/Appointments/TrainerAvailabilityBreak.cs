using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Appointments;

public class TrainerAvailabilityBreak
{
    public int Id { get; set; }

    public int TrainerAvailabilityId { get; set; }

    public TrainerAvailability TrainerAvailability { get; set; } = null!;

    [Required]
    public TimeOnly BreakStartTime { get; set; }

    [Required]
    public TimeOnly BreakEndTime { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }
}
