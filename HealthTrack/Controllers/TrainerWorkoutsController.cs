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
public class TrainerWorkoutsController(ApplicationDbContext database, PlanAccess access, NotificationService notifications) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<WorkoutPlan> OwnedPlans => access.Workouts(UserId);

    public async Task<IActionResult> Index() => View((await OwnedPlans.Include(x => x.Client).OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(x => PlanViewModel.From(x, true)).ToList());

    public async Task<IActionResult> Details(int id)
    {
        var plan = await OwnedPlans.Include(x => x.Client).Include(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == id);
        return plan is null ? NotFound() : View(PlanViewModel.From(plan, true));
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? clientId)
    {
        if (clientId is not null && !await access.Clients(UserId).AnyAsync(x => x.Id == clientId)) return NotFound();
        var model = new PlanForm { ClientId = clientId ?? string.Empty };
        await PopulateClients(model);
        ViewData["Title"] = "Create workout plan";
        return View("PlanForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlanForm model)
    {
        if (!await access.Clients(UserId).AnyAsync(x => x.Id == model.ClientId))
            ModelState.AddModelError(nameof(model.ClientId), "Choose one of your assigned clients.");
        if (!ModelState.IsValid)
        {
            await PopulateClients(model);
            ViewData["Title"] = "Create workout plan";
            return View("PlanForm", model);
        }
        var plan = new WorkoutPlan
        {
            ClientId = model.ClientId, TrainerId = UserId, PlanName = model.PlanName.Trim(),
            Description = model.Description, StartDate = model.StartDate, EndDate = model.EndDate
        };
        database.WorkoutPlans.Add(plan);
        notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Workout, assigned: true);
        await database.SaveChangesAsync();
        TempData["Success"] = "Workout plan created.";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        var model = new PlanForm
        {
            ClientId = plan.ClientId, PlanName = plan.PlanName, Description = plan.Description,
            StartDate = plan.StartDate, EndDate = plan.EndDate
        };
        await PopulateClients(model, plan.ClientId);
        ViewData["Title"] = "Edit workout plan";
        return View("PlanForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PlanForm model)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        if (model.ClientId != plan.ClientId)
            ModelState.AddModelError(nameof(model.ClientId), "A plan cannot be moved to another client.");
        if (!ModelState.IsValid)
        {
            await PopulateClients(model, plan.ClientId);
            ViewData["Title"] = "Edit workout plan";
            return View("PlanForm", model);
        }
        notifications.Plan(plan.ClientId, model.PlanName.Trim(), NotificationType.Workout);
        plan.PlanName = model.PlanName.Trim();
        plan.Description = model.Description;
        plan.StartDate = model.StartDate;
        plan.EndDate = model.EndDate;
        
        await database.SaveChangesAsync();
        TempData["Success"] = "Workout plan updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] PlanStatus status)
    {
        var plan = await OwnedPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (plan is null) return NotFound();
        if (!ModelState.IsValid || !Enum.IsDefined(status)) return BadRequest();
        if (plan.Status != status) notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Workout);
        plan.Status = status;
        await database.SaveChangesAsync();
        TempData["Success"] = $"Plan is now {status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> AddExercise(int planId)
    {
        if (!await OwnedPlans.AnyAsync(x => x.Id == planId)) return NotFound();
        ViewData["PlanId"] = planId;
        ViewData["Title"] = "Add exercise";
        return View("ExerciseForm", new ExerciseForm());
    }

    [HttpPost]
    public async Task<IActionResult> AddExercise(int planId, ExerciseForm model)
    {
        if (!await OwnedPlans.AnyAsync(x => x.Id == planId)) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["PlanId"] = planId;
            ViewData["Title"] = "Add exercise";
            return View("ExerciseForm", model);
        }
        await NotifyPlanUpdate(planId);
        database.WorkoutExercises.Add(new WorkoutExercise
        {
            WorkoutPlanId = planId,
            ExerciseName = model.ExerciseName,
            Sets = model.Sets,
            Repetitions = model.Repetitions,
            DurationMinutes = model.DurationMinutes,
            CaloriesTarget = model.CaloriesTarget,
            Instructions = model.Instructions
        });
        await database.SaveChangesAsync();
        TempData["Success"] = "Exercise added.";
        return RedirectToAction(nameof(Details), new { id = planId });
    }

    [HttpGet]
    public async Task<IActionResult> EditExercise(int id)
    {
        var item = await database.WorkoutExercises
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.WorkoutPlanId));
        if (item is null) return NotFound();
        ViewData["PlanId"] = item.WorkoutPlanId;
        ViewData["Title"] = "Edit exercise";
        return View("ExerciseForm", new ExerciseForm
        {
            ExerciseName = item.ExerciseName,
            Sets = item.Sets,
            Repetitions = item.Repetitions,
            DurationMinutes = item.DurationMinutes,
            CaloriesTarget = item.CaloriesTarget,
            Instructions = item.Instructions
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditExercise(int id, ExerciseForm model)
    {
        var item = await database.WorkoutExercises
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.WorkoutPlanId));
        if (item is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["PlanId"] = item.WorkoutPlanId;
            ViewData["Title"] = "Edit exercise";
            return View("ExerciseForm", model);
        }
        await NotifyPlanUpdate(item.WorkoutPlanId);
        item.ExerciseName = model.ExerciseName;
        item.Sets = model.Sets;
        item.Repetitions = model.Repetitions;
        item.DurationMinutes = model.DurationMinutes;
        item.CaloriesTarget = model.CaloriesTarget;
        item.Instructions = model.Instructions;
        await database.SaveChangesAsync();
        TempData["Success"] = "Exercise updated.";
        return RedirectToAction(nameof(Details), new { id = item.WorkoutPlanId });
    }

    [HttpGet, ActionName("DeleteExercise")]
    public async Task<IActionResult> ConfirmDeleteExercise(int id)
    {
        var item = await database.WorkoutExercises.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.WorkoutPlanId));
        return item is null ? NotFound() : View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            "Delete exercise", $"Delete {item.ExerciseName}? Existing workout logs will be kept.", "DeleteExercise", id, "Details", item.WorkoutPlanId));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteExercise(int id)
    {
        var item = await database.WorkoutExercises
            .FirstOrDefaultAsync(x => x.Id == id && OwnedPlans.Any(p => p.Id == x.WorkoutPlanId));
        if (item is null) return NotFound();
        await NotifyPlanUpdate(item.WorkoutPlanId);
        database.WorkoutExercises.Remove(item);
        await database.SaveChangesAsync();
        TempData["Success"] = "Exercise deleted. Existing logs have been preserved.";
        return RedirectToAction(nameof(Details), new { id = item.WorkoutPlanId });
    }

    public async Task<IActionResult> Logs(string clientId)
    {
        var client = await access.Clients(UserId).FirstOrDefaultAsync(x => x.Id == clientId);
        if (client is null) return NotFound();
        ViewData["Title"] = $"Workout history: {client.FullName}";
        var logs = await database.WorkoutLogs.Include(x => x.WorkoutPlan).Include(x => x.WorkoutExercise)
            .Where(x => x.ClientId == clientId && OwnedPlans.Any(p => p.Id == x.WorkoutPlanId))
            .OrderByDescending(x => x.WorkoutDate).ThenByDescending(x => x.Id).ToListAsync();
        return View("~/Views/Workouts/History.cshtml", logs);
    }

    private async Task NotifyPlanUpdate(int planId)
    {
        var plan = await OwnedPlans.SingleAsync(x => x.Id == planId);
        notifications.Plan(plan.ClientId, plan.PlanName, NotificationType.Workout);
    }

    private async Task PopulateClients(PlanForm model, string? clientId = null)
    {
        model.Clients = await access.Clients(UserId).Where(x => clientId == null || x.Id == clientId)
            .OrderBy(x => x.FullName).Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync();
    }
}

