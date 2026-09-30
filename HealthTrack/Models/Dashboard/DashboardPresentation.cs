using HealthTrack.Models.Appointments;

namespace HealthTrack.Models.Dashboard;

public record DashboardHeader(string Portal, string Title, string Subtitle, string? Greeting = null);

public static class DashboardPresentation
{
    public static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
}

public record DashboardAppointment(int Id, string ClientName, string TrainerName,
    DateOnly Date, TimeOnly Time, AppointmentStatus Status)
{
    public string StatusLabel => Status.ToString();
    public string StatusCss => Status switch
    {
        AppointmentStatus.Approved => "status-positive",
        AppointmentStatus.Completed => "status-neutral",
        AppointmentStatus.Cancelled => "status-muted",
        _ => "status-pending"
    };
}

public record DashboardUser(string FullName, string? Email, DateTime CreatedAt, bool IsActive);
