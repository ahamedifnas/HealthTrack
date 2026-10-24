using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Appointments;

public enum TrainerLeaveStatus
{
    Active = 1,
    Cancelled = 2
}

public class TrainerUnavailability
{
    public int Id { get; set; }

    [Required]
    public string TrainerId { get; set; } = string.Empty;

    public ApplicationUser Trainer { get; set; } = null!;

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    public bool IsFullDay { get; set; } = true;

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public TrainerLeaveStatus Status { get; set; } = TrainerLeaveStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
