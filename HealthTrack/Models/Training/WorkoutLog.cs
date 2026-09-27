using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Training;

public class WorkoutLog
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public int WorkoutPlanId { get; set; }
    public int? WorkoutExerciseId { get; set; }
    public DateOnly WorkoutDate { get; set; }
    public int DurationMinutes { get; set; }
    public int CaloriesBurned { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public bool Completed { get; set; }
    public ApplicationUser Client { get; set; } = null!;
    public WorkoutPlan WorkoutPlan { get; set; } = null!;
    public WorkoutExercise? WorkoutExercise { get; set; }
}
