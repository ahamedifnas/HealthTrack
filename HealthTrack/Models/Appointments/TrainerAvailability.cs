using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Appointments;

public class TrainerAvailability
{
    public int Id { get; set; }

    [Required]
    public string TrainerId { get; set; } = string.Empty;

    public ApplicationUser Trainer { get; set; } = null!;

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Range(15, 240)]
    public int SlotDurationMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    public DateOnly? EffectiveStartDate { get; set; }

    public DateOnly? EffectiveEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<TrainerAvailabilityBreak> Breaks { get; set; } = [];
}
