namespace HealthTrack.Models.Dashboard;

public class AdminDashboardViewModel
{
    public List<DashboardMetric> Metrics => [
        new("Total Users", TotalUsers.ToString()), new("Total Clients", TotalClients.ToString()),
        new("Total Trainers", TotalTrainers.ToString()), new("Active Users", ActiveUsers.ToString())
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
}
