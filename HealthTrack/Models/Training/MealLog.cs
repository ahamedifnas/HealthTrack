using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Training;

public class MealLog
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public int DietPlanId { get; set; }
    public DateOnly MealDate { get; set; }
    public MealType MealType { get; set; }
    [MaxLength(150)] public string FoodName { get; set; } = string.Empty;
    [MaxLength(100)] public string Quantity { get; set; } = string.Empty;
    public int Calories { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public ApplicationUser Client { get; set; } = null!;
    public DietPlan DietPlan { get; set; } = null!;
}
