namespace HealthTrack.Models.Dashboard;

public class TrainerDashboardViewModel
{
    public List<DashboardMetric> Metrics => [
        new("Total Clients", TotalClients.ToString(), TestId: "total-clients"),
        new("Active Workout Plans", ActiveWorkoutPlans.ToString(), TestId: "active-workout-plans"),
        new("Active Diet Plans", ActiveDietPlans.ToString(), TestId: "active-diet-plans")
    ];
    public int TotalClients { get; set; }
    public int ActiveWorkoutPlans { get; set; }
    public int ActiveDietPlans { get; set; }
    public List<RecentClientItem> RecentClients { get; set; } = new();
    public int TodayAppointmentCount { get; set; }
    public int PendingAppointmentCount { get; set; }
}

public record RecentClientItem(string Id, string FullName, string? Email, DateTime AssignedAt);
