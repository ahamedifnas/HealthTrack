using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Appointments;

namespace HealthTrack.Models.Reports;

public class ReportFilter : IValidatableObject
{
    [DataType(DataType.Date)] public DateOnly? FromDate { get; set; }
    [DataType(DataType.Date)] public DateOnly? ToDate { get; set; }
    [EnumDataType(typeof(AppointmentStatus))] public AppointmentStatus? Status { get; set; }
    public string Description => $"Dates: {FromDate?.ToString("yyyy-MM-dd") ?? "beginning"} to {ToDate?.ToString("yyyy-MM-dd") ?? "latest"} (inclusive)."
        + (Status is { } status ? $" Appointment status: {status}." : "");
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FromDate > ToDate) yield return new("From date must not be after To date.", [nameof(FromDate), nameof(ToDate)]);
    }
}

public enum AdminReportKind { System, Users, Clients, Trainers, HealthcareProfessionals, Appointments, WorkoutAnalytics, DietAnalytics, Engagement }
public enum PersonalReportKind { Progress, Measurements, Workout, Diet, Appointments }
