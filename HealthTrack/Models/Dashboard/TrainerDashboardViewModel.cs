namespace HealthTrack.Models.Dashboard;

public class TrainerDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public DashboardHeader Header => new("Coaching Portal", "Trainer Dashboard",
        "Manage your clients, training plans and appointments.", $"Welcome back, {DashboardPresentation.FirstName(FullName)}.");
    public List<DashboardMetric> Metrics => [
        new("Total Assigned Clients", TotalClients.ToString(), "Your active assigned clients", "total-clients"),
        new("Today's Appointments", TodayAppointmentCount.ToString(), "Excludes cancelled appointments", "today-appointments"),
        new("Pending Appointments", PendingAppointmentCount.ToString(), "Requests awaiting your review", "pending-appointments"),
        new("Active Workout Plans", ActiveWorkoutPlans.ToString(), "For your active assigned clients", "active-workout-plans"),
        new("Active Diet Plans", ActiveDietPlans.ToString(), "For your active assigned clients", "active-diet-plans")
    ];
    public int TotalClients { get; set; }
    public int ActiveWorkoutPlans { get; set; }
    public int ActiveDietPlans { get; set; }
    public List<RecentClientItem> RecentClients { get; set; } = new();
    public int TodayAppointmentCount { get; set; }
    public int PendingAppointmentCount { get; set; }
    public List<DashboardAppointment> UpcomingAppointments { get; set; } = new();
}

public record RecentClientItem(string Id, string FullName, string? Email, DateTime AssignedAt);
