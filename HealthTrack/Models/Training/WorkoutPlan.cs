using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Training;

public class WorkoutPlan
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string TrainerId { get; set; } = string.Empty;
    [MaxLength(100)] public string PlanName { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser Client { get; set; } = null!;
    public ApplicationUser Trainer { get; set; } = null!;
    public List<WorkoutExercise> Exercises { get; set; } = [];
}
