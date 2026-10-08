using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Appointments;

public enum AppointmentStatus { Pending = 1, Approved = 2, Completed = 3, Cancelled = 4 }

public class Appointment
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? TrainerId { get; set; }
    public string? HealthcareProfessionalId { get; set; }
    public HealthcareProfessional? HealthcareProfessional { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string ProviderId => TrainerId ?? HealthcareProfessionalId!;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string ProviderName => Trainer?.FullName ?? HealthcareProfessional?.User.FullName ?? "Provider unavailable";
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    [MaxLength(2000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ApplicationUser Client { get; set; } = null!;
    public ApplicationUser? Trainer { get; set; }
}
