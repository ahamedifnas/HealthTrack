using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthTrack.Models.Appointments;

public class AppointmentScheduleForm
{
    [Required, DataType(DataType.Date), Display(Name = "Appointment date")]
    public DateOnly? AppointmentDate { get; set; }

    [Required, DataType(DataType.Time), Display(Name = "Appointment time")]
    public TimeOnly? AppointmentTime { get; set; }
}

public class BookAppointmentForm : AppointmentScheduleForm
{
    [Required, Display(Name = "Trainer")] public string TrainerId { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [ValidateNever] public List<SelectListItem> Trainers { get; set; } = [];
}

public class EditAppointmentForm : AppointmentScheduleForm
{
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
