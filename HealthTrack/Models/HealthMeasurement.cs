using System.ComponentModel.DataAnnotations;
namespace HealthTrack.Models;

public class HealthMeasurement
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser Client { get; set; } = null!;
    public DateOnly MeasurementDate { get; set; }
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public decimal BMI { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public int? HeartRate { get; set; }
    public int CaloriesBurned { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
}
