using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services.Appointments;

public class TrainerAvailabilityService(
    ApplicationDbContext database,
    AppointmentClock clock,
    NotificationService notifications) : ITrainerAvailabilityService
{
    public async Task<List<AvailableTimeSlotViewModel>> GetAvailableSlotsAsync(string trainerId, DateOnly date)
    {
        var result = new List<AvailableTimeSlotViewModel>();
        var today = DateOnly.FromDateTime(clock.Now);
        var currentTime = TimeOnly.FromDateTime(clock.Now);

        if (date < today)
        {
            return result; // Past date, no slots available
        }

        // 1. Fetch active weekly schedules for this trainer on this day of week
        var schedules = await database.TrainerAvailabilities.AsNoTracking()
            .Include(x => x.Breaks)
            .Where(x => x.TrainerId == trainerId
                        && x.IsActive
                        && x.DayOfWeek == date.DayOfWeek
                        && (x.EffectiveStartDate == null || x.EffectiveStartDate <= date)
                        && (x.EffectiveEndDate == null || x.EffectiveEndDate >= date))
            .OrderBy(x => x.StartTime)
            .ToListAsync();

        if (schedules.Count == 0)
        {
            return result; // Trainer does not work on this day
        }

        // 2. Fetch active trainer leaves/unavailabilities overlapping this date
        var leaves = await database.TrainerUnavailabilities.AsNoTracking()
            .Where(x => x.TrainerId == trainerId
                        && x.Status == TrainerLeaveStatus.Active
                        && x.StartDate <= date
                        && x.EndDate >= date)
            .ToListAsync();

        // Check if full day leave is active
        var isFullDayLeave = leaves.Any(l => l.IsFullDay || (l.StartDate < date && l.EndDate > date));
        if (isFullDayLeave)
        {
            return result; // Entire day is blocked by leave
        }

        // 3. Fetch existing pending or approved appointments for this trainer on this date
        var existingAppointments = await database.Appointments.AsNoTracking()
            .Where(x => x.TrainerId == trainerId
                        && x.AppointmentDate == date
                        && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved))
            .ToListAsync();

        var bookedTimes = existingAppointments.Select(a => a.AppointmentTime).ToHashSet();

        // 4. Generate slots for each schedule
        foreach (var schedule in schedules)
        {
            var slotDuration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : 60;
            var current = schedule.StartTime;

            while (true)
            {
                var next = current.AddMinutes(slotDuration);
                // If next is before or equal to current (e.g. wrapped past midnight), break
                if (next <= current || next > schedule.EndTime)
                {
                    break;
                }

                var slot = new AvailableTimeSlotViewModel
                {
                    StartTime = current,
                    EndTime = next,
                    IsAvailable = true
                };

                // Check past time if date is today
                if (date == today && current <= currentTime)
                {
                    slot.IsAvailable = false;
                    slot.Reason = "Past time";
                }
                // Check breaks
                else if (schedule.Breaks.Any(b => current < b.BreakEndTime && next > b.BreakStartTime))
                {
                    var brk = schedule.Breaks.First(b => current < b.BreakEndTime && next > b.BreakStartTime);
                    slot.IsAvailable = false;
                    slot.Reason = string.IsNullOrWhiteSpace(brk.Description) ? "Break period" : brk.Description;
                }
                // Check partial leaves
                else if (leaves.Any(l => IsSlotInLeave(l, date, current, next)))
                {
                    var leave = leaves.First(l => IsSlotInLeave(l, date, current, next));
                    slot.IsAvailable = false;
                    slot.Reason = string.IsNullOrWhiteSpace(leave.Reason) ? "Trainer leave" : leave.Reason;
                }
                // Check booked appointments
                else if (bookedTimes.Contains(current))
                {
                    slot.IsAvailable = false;
                    slot.Reason = "Already booked";
                }

                result.Add(slot);
                current = next;
            }
        }

        return result.OrderBy(x => x.StartTime).ToList();
    }

    public async Task<bool> IsSlotAvailableAsync(string trainerId, DateOnly date, TimeOnly time)
    {
        var slots = await GetAvailableSlotsAsync(trainerId, date);
        var slot = slots.FirstOrDefault(s => s.StartTime == time);
        return slot is { IsAvailable: true };
    }

    public async Task<List<Appointment>> GetAffectedAppointmentsAsync(
        string trainerId, DateOnly startDate, DateOnly endDate, TimeOnly? startTime, TimeOnly? endTime, bool isFullDay)
    {
        var query = database.Appointments.AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Trainer)
            .Where(a => a.TrainerId == trainerId
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved)
                        && a.AppointmentDate >= startDate
                        && a.AppointmentDate <= endDate);

        var candidates = await query.ToListAsync();

        if (isFullDay)
        {
            return candidates;
        }

        // Filter partial-day matches
        return candidates.Where(a =>
        {
            if (a.AppointmentDate > startDate && a.AppointmentDate < endDate) return true;
            if (a.AppointmentDate == startDate && a.AppointmentDate == endDate)
            {
                return startTime.HasValue && endTime.HasValue
                    && a.AppointmentTime >= startTime.Value && a.AppointmentTime < endTime.Value;
            }
            if (a.AppointmentDate == startDate && startTime.HasValue)
            {
                return a.AppointmentTime >= startTime.Value;
            }
            if (a.AppointmentDate == endDate && endTime.HasValue)
            {
                return a.AppointmentTime < endTime.Value;
            }
            return false;
        }).ToList();
    }

    public async Task<int> CancelAppointmentsDueToLeaveAsync(
        string trainerId, DateOnly startDate, DateOnly endDate, TimeOnly? startTime, TimeOnly? endTime, bool isFullDay, string reason)
    {
        var affected = await database.Appointments
            .Include(a => a.Client)
            .Include(a => a.Trainer)
            .Where(a => a.TrainerId == trainerId
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved)
                        && a.AppointmentDate >= startDate
                        && a.AppointmentDate <= endDate)
            .ToListAsync();

        var matchingAppointments = affected.Where(a =>
        {
            if (isFullDay) return true;
            if (a.AppointmentDate > startDate && a.AppointmentDate < endDate) return true;
            if (a.AppointmentDate == startDate && a.AppointmentDate == endDate)
            {
                return startTime.HasValue && endTime.HasValue
                    && a.AppointmentTime >= startTime.Value && a.AppointmentTime < endTime.Value;
            }
            if (a.AppointmentDate == startDate && startTime.HasValue) return a.AppointmentTime >= startTime.Value;
            if (a.AppointmentDate == endDate && endTime.HasValue) return a.AppointmentTime < endTime.Value;
            return false;
        }).ToList();

        foreach (var appointment in matchingAppointments)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes)
                ? $"[Cancelled due to trainer leave: {reason}]"
                : $"{appointment.Notes} [Cancelled due to trainer leave: {reason}]";

            // In-app notification to client
            notifications.Appointment(appointment, appointment.ClientId, $"cancelled due to trainer leave ({reason})");
        }

        if (matchingAppointments.Count > 0)
        {
            await database.SaveChangesAsync();
        }

        return matchingAppointments.Count;
    }

    public async Task<bool> HasOverlappingAvailabilityAsync(
        string trainerId, DayOfWeek day, TimeOnly start, TimeOnly end, DateOnly? effectiveStart, DateOnly? effectiveEnd, int? excludeId = null)
    {
        var query = database.TrainerAvailabilities.AsNoTracking()
            .Where(x => x.TrainerId == trainerId
                        && x.DayOfWeek == day
                        && x.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        var existing = await query.ToListAsync();

        return existing.Any(e =>
        {
            // Check time overlap: (start < e.EndTime && end > e.StartTime)
            var timeOverlaps = start < e.EndTime && end > e.StartTime;
            if (!timeOverlaps) return false;

            // Check date window overlap
            var eStart = e.EffectiveStartDate ?? DateOnly.MinValue;
            var eEnd = e.EffectiveEndDate ?? DateOnly.MaxValue;
            var newStart = effectiveStart ?? DateOnly.MinValue;
            var newEnd = effectiveEnd ?? DateOnly.MaxValue;

            return newStart <= eEnd && newEnd >= eStart;
        });
    }

    private static bool IsSlotInLeave(TrainerUnavailability leave, DateOnly date, TimeOnly slotStart, TimeOnly slotEnd)
    {
        if (leave.IsFullDay) return true;
        if (date > leave.StartDate && date < leave.EndDate) return true;
        if (date == leave.StartDate && date == leave.EndDate)
        {
            if (leave.StartTime.HasValue && leave.EndTime.HasValue)
            {
                return slotStart < leave.EndTime.Value && slotEnd > leave.StartTime.Value;
            }
            return true;
        }
        if (date == leave.StartDate && leave.StartTime.HasValue)
        {
            return slotEnd > leave.StartTime.Value;
        }
        if (date == leave.EndDate && leave.EndTime.HasValue)
        {
            return slotStart < leave.EndTime.Value;
        }
        return false;
    }
}
