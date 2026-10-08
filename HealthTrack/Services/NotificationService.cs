using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;

namespace HealthTrack.Services;

public class NotificationService(ApplicationDbContext database, AppointmentClock clock)
{
    // Stage in the same context: the event and notification commit or roll back together.
    public void Appointment(Appointment appointment, string recipientId, string action)
        => Add(recipientId, NotificationType.Appointment, $"Appointment {action}",
            $"Your appointment on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.AppointmentTime:HH:mm} ({clock.TimeZoneId}) was {action}.");

    public void Plan(string clientId, string planName, NotificationType type, bool assigned = false)
        => Add(clientId, type, $"{type} plan {(assigned ? "assigned" : "updated")}",
            $"Your trainer has {(assigned ? "assigned" : "updated")} your {type.ToString().ToLowerInvariant()} plan: {planName}.");

    private void Add(string userId, NotificationType type, string title, string message)
        => database.Notifications.Add(new Notification
        {
            UserId = userId, Type = type, Title = title, Message = message
        });

    public void RemovedPlan(string clientId, string planName, NotificationType type, bool deactivated)
        => Add(clientId, type, $"{type} plan {(deactivated ? "deactivated" : "removed")}",
            $"Your {type.ToString().ToLowerInvariant()} plan, {planName}, was {(deactivated ? "deactivated; your recorded history is preserved" : "removed")}.");
}
