using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthTrack.Models.Appointments;

public class AvailableTimeSlotViewModel
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string FormattedTime => StartTime.ToString("hh:mm tt");
    public string FormattedRange => $"{StartTime:hh:mm tt} - {EndTime:hh:mm tt}";
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
}

public class TrainerAvailabilityForm
{
    public int Id { get; set; }

    public string? TrainerId { get; set; }
    public string? TrainerName { get; set; }

    [Required]
    [Display(Name = "Day of the Week")]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    [Display(Name = "Work Start Time")]
    public TimeOnly? StartTime { get; set; }

    [Required]
    [Display(Name = "Work End Time")]
    public TimeOnly? EndTime { get; set; }

    [Required]
    [Range(15, 240)]
    [Display(Name = "Slot Duration (Minutes)")]
    public int SlotDurationMinutes { get; set; } = 60;

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Effective Start Date (Optional)")]
    public DateOnly? EffectiveStartDate { get; set; }

    [Display(Name = "Effective End Date (Optional)")]
    public DateOnly? EffectiveEndDate { get; set; }

    public string? RowVersion { get; set; }

    public List<TrainerBreakForm> Breaks { get; set; } = [];

    public List<SelectListItem> Trainers { get; set; } = [];
}

public class TrainerBreakForm
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Break Start Time")]
    public TimeOnly? BreakStartTime { get; set; }

    [Required]
    [Display(Name = "Break End Time")]
    public TimeOnly? BreakEndTime { get; set; }

    [MaxLength(100)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

public class TrainerLeaveForm
{
    public int Id { get; set; }

    public string? TrainerId { get; set; }
    public string? TrainerName { get; set; }

    [Required]
    [Display(Name = "Start Date")]
    public DateOnly? StartDate { get; set; }

    [Required]
    [Display(Name = "End Date")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "All Day Leave")]
    public bool IsFullDay { get; set; } = true;

    [Display(Name = "Leave Start Time (if partial day)")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "Leave End Time (if partial day)")]
    public TimeOnly? EndTime { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    [Display(Name = "Reason / Notes")]
    public string Reason { get; set; } = string.Empty;

    public int AffectedAppointmentsCount { get; set; }

    public List<SelectListItem> Trainers { get; set; } = [];
}

public class TrainerScheduleOverviewViewModel
{
    public string TrainerId { get; set; } = string.Empty;
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerEmail { get; set; }
    public bool IsAdmin { get; set; }
    public List<TrainerAvailability> WeeklySchedules { get; set; } = [];
    public List<TrainerUnavailability> UpcomingLeaves { get; set; } = [];
    public List<Appointment> UpcomingAppointments { get; set; } = [];
    public List<SelectListItem> AvailableTrainers { get; set; } = [];
}

public class AvailableSlotsResponseModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsTrainer { get; set; }
    public string Date { get; set; } = string.Empty;
    public List<AvailableTimeSlotViewModel> Slots { get; set; } = [];
}
