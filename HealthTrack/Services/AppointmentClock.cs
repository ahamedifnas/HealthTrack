namespace HealthTrack.Services;

// Appointment date/time values are wall-clock times in this configured zone.
// CreatedAt remains UTC, consistent with the rest of the application.
public class AppointmentClock(IConfiguration configuration)
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Appointments:TimeZoneId"] ?? "Asia/Colombo");

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
    public string TimeZoneId => zone.Id;
}
