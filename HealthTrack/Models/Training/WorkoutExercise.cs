using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Training;

public class WorkoutExercise
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    [MaxLength(100)] public string ExerciseName { get; set; } = string.Empty;
    public int Sets { get; set; }
    public int Repetitions { get; set; }
    public int DurationMinutes { get; set; }
    public int CaloriesTarget { get; set; }
    [MaxLength(2000)] public string? Instructions { get; set; }
    public WorkoutPlan WorkoutPlan { get; set; } = null!;
}
