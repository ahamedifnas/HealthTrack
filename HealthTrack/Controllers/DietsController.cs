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
public class DietsController(ApplicationDbContext database) : Controller
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
        var model = new MealLogForm { DietPlanId = planId ?? 0 };
        await Populate(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Log(MealLogForm model)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == model.DietPlanId);
        if (plan is null) return NotFound();
        if (plan.Status != PlanStatus.Active) ModelState.AddModelError(string.Empty, "This plan is inactive and cannot receive new logs.");
        if (model.MealDate < plan.StartDate || model.MealDate > plan.EndDate || model.MealDate > DateOnly.FromDateTime(DateTime.Today))
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

    private async Task Populate(MealLogForm model)
    {
        model.Plans = await MyPlans.Where(x => x.Status == PlanStatus.Active).OrderBy(x => x.PlanName)
            .Select(x => new SelectListItem(x.PlanName, x.Id.ToString())).ToListAsync();
        if (model.DietPlanId == 0 && model.Plans.Count > 0) model.DietPlanId = int.Parse(model.Plans[0].Value);
    }
}
