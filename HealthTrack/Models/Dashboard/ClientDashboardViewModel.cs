namespace HealthTrack.Models.Dashboard;

public class ClientDashboardViewModel
{
    public DashboardHeader Header => new("Client Portal", $"Welcome, {DashboardPresentation.FirstName(FullName)}!",
        "Small steps today. A healthier you tomorrow.");
    public decimal? StartingWeight { get; set; }
    public int MeasurementCount { get; set; }
    public int UnreadNotifications { get; set; }
    public List<HealthTrack.Models.Notification> RecentNotifications { get; set; } = new();
    // Progress is the reduction in distance to the target since the first recorded weight.
    public decimal? WeightProgressPercent
    {
        get
        {
            if (StartingWeight is not { } start || CurrentWeight is not { } current || TargetWeight is not { } target)
                return null;
            if (current == target) return 100;
            var startingDistance = Math.Abs(start - target);
            if (startingDistance == 0) return null;
            return Math.Round(Math.Clamp((1 - Math.Abs(current - target) / startingDistance) * 100, 0, 100), 0);
        }
    }
    public string? WeightProgressCss => WeightProgressPercent is { } progress
        ? progress.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%" : null;
    public List<DashboardMetric> Metrics
    {
        get
        {
            var date = LatestMeasurementDate?.ToString("yyyy-MM-dd") ?? "No measurements yet";
            return [
                new("Current Weight", CurrentWeight is { } weight ? weight.ToString("0.##") + " kg" : "Not recorded", date),
                new("BMI", BMI?.ToString("0.00") ?? "Not recorded", date),
                new("Calories Burned", CaloriesBurned is { } calories ? calories + " kcal" : "Not available yet", "Today and previous 6 days"),
                new("Target Weight", TargetWeight is { } target ? target.ToString("0.##") + " kg" : "Not set", "Your personal goal")
            ];
        }
    }
    public HealthTrack.Models.Training.WorkoutPlan? WorkoutPlan { get; set; }
    public HealthTrack.Models.Training.DietPlan? DietPlan { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? BMI { get; set; }
    public long? CaloriesBurned { get; set; }
    public decimal? TargetWeight { get; set; }
    public DateOnly? LatestMeasurementDate { get; set; }
    public string FullName { get; set; } = string.Empty;
    public HealthTrack.Models.Appointments.Appointment? NextAppointment { get; set; }
}
