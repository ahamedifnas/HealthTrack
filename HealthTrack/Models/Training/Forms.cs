using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HealthTrack.Models.Training;

public class PlanForm : IValidatableObject
{
    [Required, Display(Name = "Client")] public string ClientId { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Plan name")] public string PlanName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [BindRequired, Required, DataType(DataType.Date), Display(Name = "Start date")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindRequired, Required, DataType(DataType.Date), Display(Name = "End date")]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
    [ValidateNever] public List<SelectListItem> Clients { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDate == default) yield return new("Enter a start date.", [nameof(StartDate)]);
        if (EndDate < StartDate) yield return new("End date must be on or after start date.", [nameof(EndDate)]);
    }
}

public class DietPlanForm : PlanForm
{
    [Range(1, 20000), Display(Name = "Daily calories (kcal)")] public int DailyCalories { get; set; } = 2000;
}

public class ExerciseForm
{
    [Required, StringLength(100), Display(Name = "Exercise name")] public string ExerciseName { get; set; } = string.Empty;
    [Range(1, 100)] public int Sets { get; set; } = 1;
    [Range(0, 10000)] public int Repetitions { get; set; }
    [Range(0, 1440), Display(Name = "Duration (minutes)")] public int DurationMinutes { get; set; }
    [Range(0, 20000), Display(Name = "Target calories (kcal)")] public int CaloriesTarget { get; set; }
    [StringLength(2000)] public string? Instructions { get; set; }
}

public class MealForm
{
    [EnumDataType(typeof(MealType)), Display(Name = "Meal type")] public MealType MealType { get; set; } = MealType.Breakfast;
    [Required, StringLength(150), Display(Name = "Food name")] public string FoodName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Quantity { get; set; } = string.Empty;
    [Range(0, 20000), Display(Name = "Calories (kcal)")] public int Calories { get; set; }
    [Range(typeof(decimal), "0", "10000"), Display(Name = "Protein (g)")] public decimal Protein { get; set; }
    [Range(typeof(decimal), "0", "10000"), Display(Name = "Carbohydrates (g)")] public decimal Carbohydrates { get; set; }
    [Range(typeof(decimal), "0", "10000"), Display(Name = "Fat (g)")] public decimal Fat { get; set; }
    [StringLength(2000)] public string? Instructions { get; set; }
}

public class WorkoutLogForm
{
    [ValidateNever] public string SelectedPlanName { get; set; } = string.Empty;
    [Range(1, int.MaxValue), Display(Name = "Workout plan")] public int WorkoutPlanId { get; set; }
    [Display(Name = "Exercise (optional)")] public int? WorkoutExerciseId { get; set; }
    [BindRequired, DataType(DataType.Date), Display(Name = "Workout date")] public DateOnly WorkoutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(1, 1440), Display(Name = "Duration (minutes)")] public int DurationMinutes { get; set; }
    [Range(0, 20000), Display(Name = "Calories burned (kcal)")] public int CaloriesBurned { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [ValidateNever] public List<SelectListItem> Plans { get; set; } = [];
    [ValidateNever] public List<SelectListItem> Exercises { get; set; } = [];
}

public class MealLogForm
{
    [Range(1, int.MaxValue), Display(Name = "Diet plan")] public int DietPlanId { get; set; }
    [BindRequired, DataType(DataType.Date), Display(Name = "Meal date")] public DateOnly MealDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [EnumDataType(typeof(MealType)), Display(Name = "Meal type")] public MealType MealType { get; set; } = MealType.Breakfast;
    [Required, StringLength(150), Display(Name = "Food name")] public string FoodName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Quantity { get; set; } = string.Empty;
    [Range(0, 20000), Display(Name = "Calories (kcal)")] public int Calories { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [ValidateNever] public List<SelectListItem> Plans { get; set; } = [];
}
