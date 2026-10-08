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

public class BookAppointmentForm : AppointmentScheduleForm, IValidatableObject
{
    [Display(Name = "Trainer")] public string? TrainerId { get; set; }
    [Display(Name = "Healthcare professional")] public string? HealthcareProfessionalId { get; set; }
    [ValidateNever] public List<SelectListItem> HealthcareProfessionals { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(TrainerId) == string.IsNullOrWhiteSpace(HealthcareProfessionalId))
            yield return new ValidationResult("Choose exactly one provider: a trainer OR a healthcare professional.", [nameof(TrainerId), nameof(HealthcareProfessionalId)]);
    }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [ValidateNever] public List<SelectListItem> Trainers { get; set; } = [];
    public string? RowVersion { get; set; }
}

public class EditAppointmentForm : AppointmentScheduleForm
{
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
