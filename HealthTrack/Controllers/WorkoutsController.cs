using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models.Training;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Policy = FeaturePolicies.Client)]
[AutoValidateAntiforgeryToken]
public class WorkoutsController(ApplicationDbContext database, AppointmentClock clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<WorkoutPlan> MyPlans => database.WorkoutPlans.Where(x => x.ClientId == UserId);

    public async Task<IActionResult> Index() => View((await MyPlans.Include(x => x.Trainer)
        .OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(x => PlanViewModel.From(x, false)).ToList());

    public async Task<IActionResult> Details(int id)
    {
        var plan = await MyPlans.Include(x => x.Trainer).Include(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == id);
        return plan is null ? NotFound() : View(PlanViewModel.From(plan, false));
    }

    [HttpGet]
    public async Task<IActionResult> Log(int? planId)
    {
        if (planId.HasValue && !await MyPlans.AnyAsync(x => x.Id == planId && x.Status == PlanStatus.Active)) return NotFound();
        var model = new WorkoutLogForm { WorkoutPlanId = planId ?? 0, WorkoutDate = DateOnly.FromDateTime(clock.Now) };
        await Populate(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Log(WorkoutLogForm model)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == model.WorkoutPlanId);
        if (plan is null) return NotFound();
        if (plan.Status != PlanStatus.Active) ModelState.AddModelError(string.Empty, "This plan is inactive and cannot receive new logs.");
        if (model.WorkoutDate < plan.StartDate || model.WorkoutDate > plan.EndDate || model.WorkoutDate > DateOnly.FromDateTime(clock.Now))
            ModelState.AddModelError(nameof(model.WorkoutDate), "Choose a date within the plan dates, no later than today.");
        if (model.WorkoutExerciseId.HasValue && !await database.WorkoutExercises.AnyAsync(x => x.Id == model.WorkoutExerciseId && !x.IsArchived && x.WorkoutPlanId == plan.Id))
            ModelState.AddModelError(nameof(model.WorkoutExerciseId), "Choose an exercise belonging to this plan.");
        if (!ModelState.IsValid)
        {
            await Populate(model);
            return View(model);
        }
        database.WorkoutLogs.Add(new WorkoutLog
        {
            ClientId = UserId, WorkoutPlanId = plan.Id, WorkoutExerciseId = model.WorkoutExerciseId,
            WorkoutDate = model.WorkoutDate, DurationMinutes = model.DurationMinutes,
            CaloriesBurned = model.CaloriesBurned, Notes = model.Notes, Completed = true
        });
        await database.SaveChangesAsync();
        TempData["Success"] = "Completed workout logged.";
        return RedirectToAction(nameof(History));
    }

    public async Task<IActionResult> History() { ViewData["CanLog"] = true; return View(await database.WorkoutLogs
        .Include(x => x.WorkoutPlan).Include(x => x.WorkoutExercise).Where(x => x.ClientId == UserId)
        .OrderByDescending(x => x.WorkoutDate).ThenByDescending(x => x.Id).ToListAsync()); }


    private IQueryable<WorkoutLog> OwnLogs => database.WorkoutLogs.Where(x => x.ClientId == UserId);

    [HttpGet]
    public async Task<IActionResult> LogDetails(int id)
    {
        var log = await OwnLogs.AsNoTracking().Include(x => x.WorkoutPlan).Include(x => x.WorkoutExercise).SingleOrDefaultAsync(x => x.Id == id);
        return log is null ? NotFound() : View(log);
    }

    [HttpGet]
    public async Task<IActionResult> EditLog(int id)
    {
        var log = await OwnLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        var model = new WorkoutLogForm { WorkoutPlanId = log.WorkoutPlanId, WorkoutDate = log.WorkoutDate,
            WorkoutExerciseId = log.WorkoutExerciseId, DurationMinutes = log.DurationMinutes, CaloriesBurned = log.CaloriesBurned, Completed = log.Completed, Notes = log.Notes };
        await Populate(model, log.WorkoutPlanId, log.WorkoutExerciseId);
        ViewData["IsEditing"] = true;
        return View("Log", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLog(int id, WorkoutLogForm model)
    {
        var log = await OwnLogs.Include(x => x.WorkoutPlan).SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        if (model.WorkoutPlanId != log.WorkoutPlanId) return BadRequest();
        if (model.WorkoutDate > DateOnly.FromDateTime(clock.Now)
            || (model.WorkoutDate != log.WorkoutDate && (model.WorkoutDate < log.WorkoutPlan.StartDate || model.WorkoutDate > log.WorkoutPlan.EndDate)))
            ModelState.AddModelError(nameof(model.WorkoutDate), "Choose a date within the plan dates, no later than today.");
        if (model.WorkoutExerciseId.HasValue && !await database.WorkoutExercises.AnyAsync(x => x.Id == model.WorkoutExerciseId && (!x.IsArchived || x.Id == log.WorkoutExerciseId) && x.WorkoutPlanId == log.WorkoutPlanId))
            ModelState.AddModelError(nameof(model.WorkoutExerciseId), "Choose an exercise belonging to this plan.");
        if (!ModelState.IsValid)
        {
            await Populate(model, log.WorkoutPlanId, log.WorkoutExerciseId);
            ViewData["IsEditing"] = true;
            return View("Log", model);
        }
        log.WorkoutDate = model.WorkoutDate;
        log.WorkoutExerciseId = model.WorkoutExerciseId; log.DurationMinutes = model.DurationMinutes;
        log.CaloriesBurned = model.CaloriesBurned; log.Completed = model.Completed;
        log.Notes = model.Notes?.Trim();
        await database.SaveChangesAsync();
        TempData["Success"] = "Workout log updated.";
        return RedirectToAction(nameof(LogDetails), new { id });
    }

    [HttpGet, ActionName("DeleteLog")]
    public async Task<IActionResult> ConfirmDeleteLog(int id)
    {
        var log = await OwnLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return log is null ? NotFound() : View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            "Delete workout log", $"Permanently delete your workout log from {log.WorkoutDate:yyyy-MM-dd}?",
            "DeleteLog", id, "LogDetails", id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLog(int id)
    {
        var log = await OwnLogs.SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        database.WorkoutLogs.Remove(log);
        await database.SaveChangesAsync();
        TempData["Success"] = "Workout log deleted.";
        return RedirectToAction(nameof(History));
    }

    private async Task Populate(WorkoutLogForm model, int? existingPlanId = null, int? existingExerciseId = null)
    {
        model.Plans = await MyPlans.Where(x => x.Status == PlanStatus.Active || x.Id == existingPlanId).OrderBy(x => x.PlanName)
            .Select(x => new SelectListItem(x.PlanName, x.Id.ToString())).ToListAsync();
        if (model.WorkoutPlanId == 0 && model.Plans.Count > 0) model.WorkoutPlanId = int.Parse(model.Plans[0].Value);
        foreach (var item in model.Plans) item.Selected = item.Value == model.WorkoutPlanId.ToString();
        model.SelectedPlanName = model.Plans.FirstOrDefault(x => x.Selected)?.Text ?? string.Empty;
        model.Exercises = await database.WorkoutExercises.Where(x => x.WorkoutPlanId == model.WorkoutPlanId && (!x.IsArchived || x.Id == existingExerciseId) && MyPlans.Any(p => p.Id == x.WorkoutPlanId))
            .OrderBy(x => x.ExerciseName).Select(x => new SelectListItem(x.ExerciseName, x.Id.ToString())).ToListAsync();
    }
}
