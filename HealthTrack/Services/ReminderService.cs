using System.Data;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Training;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class ReminderService(IServiceScopeFactory scopes, AppointmentClock clock, ILogger<ReminderService> logger)
{
    public async Task GenerateAsync(string userId, bool client)
    {
        // Isolate reminder writes from the request's business transaction/change tracker.
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var now = clock.Now;
            var today = DateOnly.FromDateTime(now);
            var tomorrow = today.AddDays(1);
            var time = TimeOnly.FromDateTime(now);
            var appointments = await database.Appointments.AsNoTracking()
                .Where(x => (x.ClientId == userId || x.TrainerId == userId || x.HealthcareProfessionalId == userId)
                    && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved)
                    && (x.AppointmentDate == tomorrow || (x.AppointmentDate == today && x.AppointmentTime >= time)))
                .OrderBy(x => x.Id).ToListAsync();
            foreach (var appointment in appointments)
            {
                var day = appointment.AppointmentDate == today ? "today" : "tomorrow";
                // A rescheduled slot is a new reminder; repeated reads of the same slot are not.
                var key = $"appointment:{appointment.Id}:{appointment.AppointmentDate:yyyyMMdd}:{appointment.AppointmentTime:HHmm}:{day}";
                var message = $"Appointment Reminder: You have an appointment {day} at {appointment.AppointmentTime:hh:mm tt} ({clock.TimeZoneId}).";
                foreach (var recipient in new[] { appointment.ClientId, appointment.ProviderId }.Order())
                    await AddAsync(recipient, key, NotificationType.Appointment, "Appointment Reminder", message);
            }
            if (client)
            {
                if (await database.WorkoutPlans.AnyAsync(x => x.ClientId == userId && x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today))
                    await AddAsync(userId, $"workout:{today:yyyyMMdd}", NotificationType.Workout, "Workout Reminder", "Workout Reminder: Don't forget to complete today's workout.");
                if (await database.DietPlans.AnyAsync(x => x.ClientId == userId && x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today))
                    await AddAsync(userId, $"meal:{today:yyyyMMdd}", NotificationType.Diet, "Meal Reminder", "Meal Reminder: Remember to follow and record today's meal plan.");
            }
            await database.SaveChangesAsync();
            await transaction.CommitAsync();

            async Task AddAsync(string recipient, string key, NotificationType type, string title, string message)
            {
                if (!await database.Users.AnyAsync(x => x.Id == recipient && x.IsActive)
                    || await database.ReminderDeliveries.AnyAsync(x => x.UserId == recipient && x.ReminderKey == key)) return;
                database.ReminderDeliveries.Add(new ReminderDelivery { UserId = recipient, ReminderKey = key });
                database.Notifications.Add(new Notification { UserId = recipient, Type = type, Title = title, Message = message });
            }
        }
        catch (Exception ex) when (IsConcurrentDeliveryFailure(ex))
        {
            // Another request won or this transaction was the deadlock victim. The next GET retries.
            // Disposal rolls back BOTH delivery records and notifications.
            logger.LogInformation("Concurrent reminder generation deferred until the next request.");
        }
    }

    private static bool IsConcurrentDeliveryFailure(Exception exception)
    {
        // SQL Server's EF execution strategy can wrap a deadlock in an
        // InvalidOperationException outside DbUpdateException. Inspect the chain
        // without swallowing unrelated failures. The entire transaction is disposed
        // before the page continues, so a delivery and its notification stay atomic.
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 1205 or 2601 or 2627 }) return true;
        return false;
    }
}
