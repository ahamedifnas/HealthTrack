namespace HealthTrack.Models.Appointments;

public class AppointmentDetailsViewModel
{
    public required Appointment Appointment { get; init; }
    public required string TimeZoneId { get; init; }
    public bool IsAdmin { get; init; }
    public bool CanClientCancel { get; init; }
    public bool CanManage { get; init; }
    public bool CanApprove { get; init; }
    public bool CanComplete { get; init; }
    public bool AwaitingScheduledTime { get; init; }
}

public record AppointmentListViewModel(List<Appointment> Appointments, string Description, bool CanBook, string TimeZoneId);
