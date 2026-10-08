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
public class DietsController(ApplicationDbContext database, AppointmentClock clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<DietPlan> MyPlans => database.DietPlans.Where(x => x.ClientId == UserId);

    public async Task<IActionResult> Index() => View((await MyPlans.Include(x => x.Trainer)
        .OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(x => PlanViewModel.From(x, false)).ToList());

    public async Task<IActionResult> Details(int id)
    {
        var plan = await MyPlans.Include(x => x.Trainer).Include(x => x.Meals).FirstOrDefaultAsync(x => x.Id == id);
        return plan is null ? NotFound() : View(PlanViewModel.From(plan, false));
    }

    [HttpGet]
    public async Task<IActionResult> Log(int? planId)
    {
        if (planId.HasValue && !await MyPlans.AnyAsync(x => x.Id == planId && x.Status == PlanStatus.Active)) return NotFound();
        var model = new MealLogForm { DietPlanId = planId ?? 0, MealDate = DateOnly.FromDateTime(clock.Now) };
        await Populate(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Log(MealLogForm model)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == model.DietPlanId);
        if (plan is null) return NotFound();
        if (plan.Status != PlanStatus.Active) ModelState.AddModelError(string.Empty, "This plan is inactive and cannot receive new logs.");
        if (model.MealDate < plan.StartDate || model.MealDate > plan.EndDate || model.MealDate > DateOnly.FromDateTime(clock.Now))
            ModelState.AddModelError(nameof(model.MealDate), "Choose a date within the plan dates, no later than today.");
        if (!ModelState.IsValid)
        {
            await Populate(model);
            return View(model);
        }
        database.MealLogs.Add(new MealLog
        {
            ClientId = UserId, DietPlanId = plan.Id, MealDate = model.MealDate, MealType = model.MealType,
            FoodName = model.FoodName.Trim(), Quantity = model.Quantity.Trim(), Calories = model.Calories, Notes = model.Notes
        });
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal logged.";
        return RedirectToAction(nameof(History));
    }

    public async Task<IActionResult> History() { ViewData["CanLog"] = true; return View(await database.MealLogs.Include(x => x.DietPlan)
        .Where(x => x.ClientId == UserId).OrderByDescending(x => x.MealDate).ThenByDescending(x => x.Id).ToListAsync()); }


    private IQueryable<MealLog> OwnLogs => database.MealLogs.Where(x => x.ClientId == UserId);

    [HttpGet]
    public async Task<IActionResult> LogDetails(int id)
    {
        var log = await OwnLogs.AsNoTracking().Include(x => x.DietPlan).SingleOrDefaultAsync(x => x.Id == id);
        return log is null ? NotFound() : View(log);
    }

    [HttpGet]
    public async Task<IActionResult> EditLog(int id)
    {
        var log = await OwnLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        var model = new MealLogForm { DietPlanId = log.DietPlanId, MealDate = log.MealDate,
            MealType = log.MealType, FoodName = log.FoodName, Quantity = log.Quantity, Calories = log.Calories, Notes = log.Notes };
        await Populate(model, log.DietPlanId);
        ViewData["IsEditing"] = true;
        return View("Log", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLog(int id, MealLogForm model)
    {
        var log = await OwnLogs.Include(x => x.DietPlan).SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        if (model.DietPlanId != log.DietPlanId) return BadRequest();
        if (model.MealDate > DateOnly.FromDateTime(clock.Now)
            || (model.MealDate != log.MealDate && (model.MealDate < log.DietPlan.StartDate || model.MealDate > log.DietPlan.EndDate)))
            ModelState.AddModelError(nameof(model.MealDate), "Choose a date within the plan dates, no later than today.");
        
        if (!ModelState.IsValid)
        {
            await Populate(model, log.DietPlanId);
            ViewData["IsEditing"] = true;
            return View("Log", model);
        }
        log.MealDate = model.MealDate;
        log.MealType = model.MealType; log.FoodName = model.FoodName.Trim();
        log.Quantity = model.Quantity.Trim(); log.Calories = model.Calories;
        log.Notes = model.Notes?.Trim();
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal log updated.";
        return RedirectToAction(nameof(LogDetails), new { id });
    }

    [HttpGet, ActionName("DeleteLog")]
    public async Task<IActionResult> ConfirmDeleteLog(int id)
    {
        var log = await OwnLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return log is null ? NotFound() : View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            "Delete meal log", $"Permanently delete your meal log from {log.MealDate:yyyy-MM-dd}?",
            "DeleteLog", id, "LogDetails", id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLog(int id)
    {
        var log = await OwnLogs.SingleOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();
        database.MealLogs.Remove(log);
        await database.SaveChangesAsync();
        TempData["Success"] = "Meal log deleted.";
        return RedirectToAction(nameof(History));
    }

    private async Task Populate(MealLogForm model, int? existingPlanId = null)
    {
        model.Plans = await MyPlans.Where(x => x.Status == PlanStatus.Active || x.Id == existingPlanId).OrderBy(x => x.PlanName)
            .Select(x => new SelectListItem(x.PlanName, x.Id.ToString())).ToListAsync();
        if (model.DietPlanId == 0 && model.Plans.Count > 0) model.DietPlanId = int.Parse(model.Plans[0].Value);
    }
}
