using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Training;

public class DietMeal
{
    public int Id { get; set; }
    public int DietPlanId { get; set; }
    public MealType MealType { get; set; }
    [MaxLength(150)] public string FoodName { get; set; } = string.Empty;
    [MaxLength(100)] public string Quantity { get; set; } = string.Empty;
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    [MaxLength(2000)] public string? Instructions { get; set; }
    public DietPlan DietPlan { get; set; } = null!;
}
