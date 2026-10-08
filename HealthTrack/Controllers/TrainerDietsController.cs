using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Training;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Policy = FeaturePolicies.Trainer)]
[AutoValidateAntiforgeryToken]
public class TrainerDietsController(ApplicationDbContext database, PlanAccess access, NotificationService notifications) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<DietPlan> OwnedPlans => access.Diets(UserId);

    public async Task<IActionResult> Index() => View((await OwnedPlans.Include(x => x.Client).OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(x => PlanViewModel.From(x, true)).ToList());

    public async Task<IActionResult> Details(int id)
    {
        var plan = await OwnedPlans.Include(x => x.Client).Include(x => x.Meals).FirstOrDefaultAsync(x => x.Id == id);
        return plan is null ? NotFound() : View(PlanViewModel.From(plan, true));
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? clientId)
    {
        if (clientId is not null && !await access.Clients(UserId).AnyAsync(x => x.Id == clientId)) return NotFound();
        var model = new DietPlanForm { ClientId = clientId ?? string.Empty };
        await PopulateClients(model);
        ViewData["Title"] = "Create diet plan";
        return View("PlanForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(DietPlanForm model)
    {
        if (!await access.Clients(UserId).AnyAsync(x => x.Id == model.ClientId))
            ModelState.AddModelError(nameof(model.ClientId), "Choose one of your assigned clients.");
        if (!ModelState.IsValid)
        {
            await PopulateClients(model);
            ViewData["Title"] = "Create diet plan";
            return View("PlanForm", model);
        }
        var plan = new DietPlan
        {
            ClientId = model.ClientId, TrainerId = UserId, PlanName = model.PlanName.Trim(),
            Description = model.Description, StartDate = model.StartDate, EndDate = model.EndDate, DailyCalories = model.DailyCalories
        };
        database.DietPlans.Add(plan);
        notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Diet, assigned: true);
        await database.SaveChangesAsync();
        TempData["Success"] = "Diet plan created.";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        var model = new DietPlanForm
        {
            ClientId = plan.ClientId, PlanName = plan.PlanName, Description = plan.Description,
            StartDate = plan.StartDate, EndDate = plan.EndDate, Status = plan.Status, DailyCalories = plan.DailyCalories
        };
        await PopulateClients(model, plan.ClientId);
        ViewData["Title"] = "Edit diet plan";
        return View("PlanForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DietPlanForm model)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        if (model.ClientId != plan.ClientId)
            ModelState.AddModelError(nameof(model.ClientId), "A plan cannot be moved to another client.");
        if (!ModelState.IsValid)
        {
            await PopulateClients(model, plan.ClientId);
            ViewData["Title"] = "Edit diet plan";
            return View("PlanForm", model);
        }
        notifications.Plan(plan.ClientId, model.PlanName.Trim(), NotificationType.Diet);
        plan.PlanName = model.PlanName.Trim();
        plan.Description = model.Description;
        plan.StartDate = model.StartDate;
        plan.EndDate = model.EndDate;
        if (model.Status is { } status) plan.Status = status;
        plan.DailyCalories = model.DailyCalories;
        await database.SaveChangesAsync();
        TempData["Success"] = "Diet plan updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] PlanStatus status)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        if (!ModelState.IsValid || !Enum.IsDefined(status)) return BadRequest();
        if (plan.Status != status) notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Diet);
        plan.Status = status;
        await database.SaveChangesAsync();
        TempData["Success"] = $"Plan is now {status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> AddMeal(int planId)
    {
        if (!await OwnedPlans.AnyAsync(x => x.Id == planId)) return NotFound();
        ViewData["PlanId"] = planId;
        ViewData["Title"] = "Add meal";
        return View("MealForm", new MealForm());
    }

    [HttpPost]
    public async Task<IActionResult> AddMeal(int planId, MealForm model)
    {
        if (!await OwnedPlans.AnyAsync(x => x.Id == planId)) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["PlanId"] = planId;
            ViewData["Title"] = "Add meal";
            return View("MealForm", model);
        }
        await NotifyPlanUpdate(planId);
        database.DietMeals.Add(new DietMeal
        {
            DietPlanId = planId,
            MealType = model.MealType,
            FoodName = model.FoodName,
            Quantity = model.Quantity,
            Calories = model.Calories,
            Protein = model.Protein,
            Carbohydrates = model.Carbohydrates,
            Fat = model.Fat,
            Instructions = model.Instructions
        });
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal added.";
        return RedirectToAction(nameof(Details), new { id = planId });
    }

    [HttpGet]
    public async Task<IActionResult> EditMeal(int id)
    {
        var item = await database.DietMeals
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.DietPlanId));
        if (item is null) return NotFound();
        ViewData["PlanId"] = item.DietPlanId;
        ViewData["Title"] = "Edit meal";
        return View("MealForm", new MealForm
        {
            MealType = item.MealType,
            FoodName = item.FoodName,
            Quantity = item.Quantity,
            Calories = item.Calories,
            Protein = item.Protein,
            Carbohydrates = item.Carbohydrates,
            Fat = item.Fat,
            Instructions = item.Instructions
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditMeal(int id, MealForm model)
    {
        var item = await database.DietMeals
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.DietPlanId));
        if (item is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["PlanId"] = item.DietPlanId;
            ViewData["Title"] = "Edit meal";
            return View("MealForm", model);
        }
        await NotifyPlanUpdate(item.DietPlanId);
        item.MealType = model.MealType;
        item.FoodName = model.FoodName;
        item.Quantity = model.Quantity;
        item.Calories = model.Calories;
        item.Protein = model.Protein;
        item.Carbohydrates = model.Carbohydrates;
        item.Fat = model.Fat;
        item.Instructions = model.Instructions;
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal updated.";
        return RedirectToAction(nameof(Details), new { id = item.DietPlanId });
    }

    [HttpGet, ActionName("DeleteMeal")]
    public async Task<IActionResult> ConfirmDeleteMeal(int id)
    {
        var item = await database.DietMeals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.DietPlanId));
        return item is null ? NotFound() : View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            "Delete meal", $"Delete {item.FoodName}? Existing meal logs will be kept.", "DeleteMeal", id, "Details", item.DietPlanId));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMeal(int id)
    {
        var item = await database.DietMeals
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.DietPlanId));
        if (item is null) return NotFound();
        await NotifyPlanUpdate(item.DietPlanId);
        database.DietMeals.Remove(item);
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal deleted. Existing logs have been preserved.";
        return RedirectToAction(nameof(Details), new { id = item.DietPlanId });
    }


    [HttpGet]
    public async Task<IActionResult> MealDetails(int id)
    {
        var item = await database.DietMeals.AsNoTracking().Include(x => x.DietPlan)
            .SingleOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.DietPlanId));
        return item is null ? NotFound() : View("MealDetails", item);
    }

    [HttpGet, ActionName("Delete")]
    public async Task<IActionResult> ConfirmDelete(int id)
    {
        var plan = await OwnedPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        var history = await database.MealLogs.AnyAsync(x => x.DietPlanId == id);
        return View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            history ? "Deactivate diet plan" : "Delete diet plan",
            history ? $"Deactivate {plan.PlanName}? Its logs and history will be preserved."
                : $"Delete {plan.PlanName} and its meals? If logs have been added, the plan will be deactivated instead.",
            "Delete", id, "Details", id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var plan = await OwnedPlans.Include(x => x.Meals).SingleOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        var history = await database.MealLogs.AnyAsync(x => x.DietPlanId == id);
        if (history) plan.Status = PlanStatus.Inactive;
        else
        {
            database.DietMeals.RemoveRange(plan.Meals);
            database.DietPlans.Remove(plan);
        }
        notifications.RemovedPlan(plan.ClientId, plan.PlanName, NotificationType.Diet, history);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = history ? "Plan deactivated. All logs and history have been preserved." : "Plan deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyPlanUpdate(int planId)
    {
        var plan = await OwnedPlans.SingleAsync(x => x.Id == planId);
        notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Diet);
    }

    [HttpGet]
    public async Task<IActionResult> Logs(string clientId)
    {
        var client = await access.Clients(UserId).FirstOrDefaultAsync(x => x.Id == clientId);
        if (client is null) return NotFound();
        ViewData["Title"] = $"Meal history: {client.FullName}";
        return View("~/Views/Diets/History.cshtml", await database.MealLogs.AsNoTracking().Include(x => x.DietPlan)
            .Where(x => x.ClientId == clientId && OwnedPlans.Any(p => p.Id == x.DietPlanId))
            .OrderByDescending(x => x.MealDate).ThenByDescending(x => x.Id).ToListAsync());
    }

    private async Task PopulateClients(PlanForm model, string? clientId = null)
    {
        model.Clients = await access.Clients(UserId).Where(x => clientId == null || x.Id == clientId)
            .OrderBy(x => x.FullName).Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync();
    }
}

