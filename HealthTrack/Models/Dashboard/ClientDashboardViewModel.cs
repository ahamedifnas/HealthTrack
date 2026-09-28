namespace HealthTrack.Models.Dashboard;

public class ClientDashboardViewModel
{
    public List<DashboardMetric> Metrics
    {
        get
        {
            var date = LatestMeasurementDate?.ToString("yyyy-MM-dd") ?? "No measurements yet";
            return [
                new("Current Weight", CurrentWeight is { } weight ? weight.ToString("0.##") + " kg" : "Not recorded", date),
                new("BMI", BMI?.ToString("0.00") ?? "Not recorded", date),
                new("Calories Burned", CaloriesBurned + " kcal", "Today and previous 6 days"),
                new("Target Weight", TargetWeight is { } target ? target.ToString("0.##") + " kg" : "Not set", "Your personal goal")
            ];
        }
    }
    public HealthTrack.Models.Training.WorkoutPlan? WorkoutPlan { get; set; }
    public HealthTrack.Models.Training.DietPlan? DietPlan { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? BMI { get; set; }
    public long CaloriesBurned { get; set; }
    public decimal? TargetWeight { get; set; }
    public DateOnly? LatestMeasurementDate { get; set; }
    public string FullName { get; set; } = string.Empty;
    public HealthTrack.Models.Appointments.Appointment? NextAppointment { get; set; }
}
