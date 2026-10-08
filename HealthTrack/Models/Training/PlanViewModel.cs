namespace HealthTrack.Models.Training;

// Read-only presentation data. Mutation actions continue to authorize against database entities.
public class PlanViewModel
{
    public int Id { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string TrainerName { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public PlanStatus Status { get; init; }
    public int DailyCalories { get; init; }
    public bool CanManage { get; init; }
    public bool CanLog => !CanManage && Status == PlanStatus.Active;
    public string StatusCss => Status == PlanStatus.Active ? "bg-success" : "bg-secondary";
    public PlanStatus NextStatus => Status == PlanStatus.Active ? PlanStatus.Inactive : PlanStatus.Active;
    public string StatusAction => Status == PlanStatus.Active ? "Deactivate" : "Activate";
    public List<WorkoutExercise> Exercises { get; init; } = [];
    public List<MealGroupViewModel> MealGroups { get; init; } = [];

    public static PlanViewModel From(WorkoutPlan plan, bool manage) => new()
    {
        Id = plan.Id, ClientId = plan.ClientId, ClientName = plan.Client?.FullName ?? string.Empty,
        TrainerName = plan.Trainer?.FullName ?? string.Empty, PlanName = plan.PlanName,
        Description = plan.Description, StartDate = plan.StartDate, EndDate = plan.EndDate,
        Status = plan.Status, CanManage = manage, Exercises = plan.Exercises.Where(x => !x.IsArchived).OrderBy(x => x.Id).ToList()
    };

    public static PlanViewModel From(DietPlan plan, bool manage) => new()
    {
        Id = plan.Id, ClientId = plan.ClientId, ClientName = plan.Client?.FullName ?? string.Empty,
        TrainerName = plan.Trainer?.FullName ?? string.Empty, PlanName = plan.PlanName,
        Description = plan.Description, StartDate = plan.StartDate, EndDate = plan.EndDate,
        Status = plan.Status, CanManage = manage, DailyCalories = plan.DailyCalories,
        MealGroups = plan.Meals.GroupBy(x => x.MealType).OrderBy(x => x.Key)
            .Select(x => new MealGroupViewModel(x.Key, x.OrderBy(m => m.Id).ToList())).ToList()
    };
}

public record MealGroupViewModel(MealType Type, List<DietMeal> Meals);
