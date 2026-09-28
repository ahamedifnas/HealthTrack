using System.ComponentModel.DataAnnotations;
namespace HealthTrack.Models.Measurements;

public class AddMeasurementViewModel
{
    [Required, DataType(DataType.Date), Display(Name = "Measurement date")]
    public DateOnly? MeasurementDate { get; set; }
    [Required, Range(typeof(decimal), "1", "500"), Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }
    [Required, Range(typeof(decimal), "30", "300"), Display(Name = "Height (cm)")]
    public decimal? HeightCm { get; set; }
    [Range(typeof(decimal), "0", "100"), Display(Name = "Body fat (%)")]
    public decimal? BodyFatPercentage { get; set; }
    [Range(1, 300), Display(Name = "Heart rate (bpm)")]
    public int? HeartRate { get; set; }
    [Required, Range(0, 50000), Display(Name = "Calories burned (kcal)")]
    public int? CaloriesBurned { get; set; } = 0;
    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class TargetWeightViewModel
{
    [Range(typeof(decimal), "1", "500"), Display(Name = "Target weight (kg)")]
    public decimal? TargetWeight { get; set; }
}

public class ProgressViewModel
{
    public string FullName { get; set; } = string.Empty;
    public decimal? TargetWeight { get; set; }
    public bool IsOwnProgress { get; set; }
    public List<HealthMeasurement> Measurements { get; set; } = new();
    public HealthMeasurement? Latest => Measurements.FirstOrDefault();
    public List<ProgressTrendRow> Trends { get; set; } = new();
}

public record ProgressTrendRow(DateOnly Date, decimal Weight, decimal BMI, int Calories,
    decimal? WeightChange, decimal? BMIChange);

public class HealthSummaryViewModel
{
    public int TotalMeasurements { get; set; }
    public int ClientsWithMeasurements { get; set; }
    public int ClientsWithTargets { get; set; }
    public long RecentCaloriesBurned { get; set; }
}
