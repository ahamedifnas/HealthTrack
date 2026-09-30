namespace HealthTrack.Models.Dashboard;

public class AdminDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public DashboardHeader Header => new("Admin Portal", "HealthTrack Administration",
        "Monitor users, trainers, appointments and system activity.", $"Welcome back, {DashboardPresentation.FirstName(FullName)}.");
    public List<DashboardMetric> Metrics => [
        new("Total Users", TotalUsers.ToString(), "Registered accounts"), new("Total Clients", TotalClients.ToString(), "Client accounts"),
        new("Total Trainers", TotalTrainers.ToString(), "Trainer accounts"), new("Active Users", ActiveUsers.ToString(), "Accounts enabled for access"),
        new("Total Appointments", TotalAppointments.ToString(), "Across all appointment statuses", "total-appointments"),
        new("Active Workout Plans", ActiveWorkoutPlans.ToString(), "Plans marked active"),
        new("Active Diet Plans", ActiveDietPlans.ToString(), "Plans marked active")
    ];
    public List<HealthTrack.Models.Admin.TrainerListItem> Trainers { get; set; } = new();
    public HealthTrack.Models.Admin.AssignClientViewModel Assignment { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AssignmentClients { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AssignmentTrainers { get; set; } = new();
    public List<HealthTrack.Models.Admin.AssignmentListItem> Assignments { get; set; } = new();
    public int TotalUsers { get; set; }
    public int TotalClients { get; set; }
    public int TotalTrainers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalAppointments { get; set; }
    public int ActiveWorkoutPlans { get; set; }
    public int ActiveDietPlans { get; set; }
    public List<DashboardUser> RecentUsers { get; set; } = new();
    public List<DashboardAppointment> RecentAppointments { get; set; } = new();
    public List<HealthTrack.Models.Admin.AssignmentListItem> RecentAssignments => Assignments.Take(5).ToList();
}
