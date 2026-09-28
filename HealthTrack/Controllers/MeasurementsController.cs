using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Measurements;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Policy = FeaturePolicies.Client)]
[AutoValidateAntiforgeryToken]
public class MeasurementsController(ApplicationDbContext database, UserManager<ApplicationUser> users, AppointmentClock clock) : Controller
{
    private string ClientId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await database.HealthMeasurements.AsNoTracking()
        .Where(x => x.ClientId == ClientId).OrderByDescending(x => x.MeasurementDate).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new AddMeasurementViewModel { MeasurementDate = DateOnly.FromDateTime(clock.Now) });

    [HttpPost]
    public async Task<IActionResult> Create(AddMeasurementViewModel model)
    {
        var today = DateOnly.FromDateTime(clock.Now);
        if (model.MeasurementDate is { } date && (date > today || date < new DateOnly(1900, 1, 1)))
            ModelState.AddModelError(nameof(model.MeasurementDate), "Choose a date between 1900-01-01 and today.");
        if (!ModelState.IsValid) return View(model);
        if (await database.HealthMeasurements.AnyAsync(x => x.ClientId == ClientId && x.MeasurementDate == model.MeasurementDate))
        {
            ModelState.AddModelError(nameof(model.MeasurementDate), "A measurement already exists for this date.");
            return View(model);
        }
        var weight = decimal.Round(model.WeightKg!.Value, 2, MidpointRounding.AwayFromZero);
        var height = decimal.Round(model.HeightCm!.Value, 2, MidpointRounding.AwayFromZero);
        var heightMetres = height / 100m;
        database.HealthMeasurements.Add(new HealthMeasurement
        {
            ClientId = ClientId, MeasurementDate = model.MeasurementDate!.Value,
            WeightKg = weight, HeightCm = height,
            BMI = decimal.Round(weight / (heightMetres * heightMetres), 2, MidpointRounding.AwayFromZero),
            BodyFatPercentage = model.BodyFatPercentage, HeartRate = model.HeartRate,
            CaloriesBurned = model.CaloriesBurned!.Value, Notes = model.Notes?.Trim()
        });
        try { await database.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(model.MeasurementDate), "A measurement already exists for this date.");
            return View(model);
        }
        TempData["Success"] = "Health measurement saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> TargetWeight()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new TargetWeightViewModel { TargetWeight = user.TargetWeight });
    }

    [HttpPost]
    public async Task<IActionResult> TargetWeight(TargetWeightViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        user.TargetWeight = model.TargetWeight is { } target ? decimal.Round(target, 2, MidpointRounding.AwayFromZero) : null;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        TempData["Success"] = "Target weight updated.";
        return RedirectToAction("Client", "Dashboard");
    }
}
