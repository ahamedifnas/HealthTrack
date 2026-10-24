using HealthTrack.Models.Appointments;

namespace HealthTrack.Services.Appointments;

public interface ITrainerAvailabilityService
{
    Task<List<AvailableTimeSlotViewModel>> GetAvailableSlotsAsync(string trainerId, DateOnly date);
    Task<bool> IsSlotAvailableAsync(string trainerId, DateOnly date, TimeOnly time);
    Task<List<Appointment>> GetAffectedAppointmentsAsync(string trainerId, DateOnly startDate, DateOnly endDate, TimeOnly? startTime, TimeOnly? endTime, bool isFullDay);
    Task<int> CancelAppointmentsDueToLeaveAsync(string trainerId, DateOnly startDate, DateOnly endDate, TimeOnly? startTime, TimeOnly? endTime, bool isFullDay, string reason);
    Task<bool> HasOverlappingAvailabilityAsync(string trainerId, DayOfWeek day, TimeOnly start, TimeOnly end, DateOnly? effectiveStart, DateOnly? effectiveEnd, int? excludeId = null);
}
