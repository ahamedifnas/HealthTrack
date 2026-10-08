using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Measurements;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class ProgressController(ApplicationDbContext database, PlanAccess access, AppointmentClock clock) : Controller
{
    public async Task<IActionResult> Index(string? clientId = null)
    {
        if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.HealthcareProfessional)) return Forbid();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ApplicationUser? client;
        var own = false;
        if (User.IsInRole(AppRoles.Trainer))
        {
            if (string.IsNullOrEmpty(clientId)) return RedirectToAction("Index", "Clients");
            client = await access.Clients(userId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId);
        }
        else if (User.IsInRole(AppRoles.Client))
        {
            if (clientId is not null && clientId != userId) return NotFound();
            client = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
            own = true;
        }
        else return Forbid();
        if (client is null) return NotFound();
        ViewData["CanManageMeasurements"] = own;
        var measurements = await database.HealthMeasurements.AsNoTracking().Where(x => x.ClientId == client.Id)
            .OrderBy(x => x.MeasurementDate).ToListAsync();
        var trends = new List<ProgressTrendRow>();
        HealthMeasurement? previous = null;
        foreach (var measurement in measurements)
        {
            trends.Add(new ProgressTrendRow(measurement.MeasurementDate, measurement.WeightKg, measurement.BMI,
                measurement.CaloriesBurned, previous is null ? null : measurement.WeightKg - previous.WeightKg,
                previous is null ? null : measurement.BMI - previous.BMI));
            previous = measurement;
        }
        return View(new ProgressViewModel
        {
            FullName = client.FullName, TargetWeight = client.TargetWeight, IsOwnProgress = own,
            Measurements = measurements.OrderByDescending(x => x.MeasurementDate).ToList(),
            Trends = trends
        });
    }

    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Summary()
    {
        var today = DateOnly.FromDateTime(clock.Now);
        var since = today.AddDays(-6);
        return View(new HealthSummaryViewModel
        {
            TotalMeasurements = await database.HealthMeasurements.CountAsync(),
            ClientsWithMeasurements = await database.HealthMeasurements.Select(x => x.ClientId).Distinct().CountAsync(),
            ClientsWithTargets = await database.Users.CountAsync(x => x.TargetWeight != null && database.UserRoles.Any(ur =>
                ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Client))),
            RecentCaloriesBurned = await database.HealthMeasurements.Where(x => x.MeasurementDate >= since && x.MeasurementDate <= today)
                .SumAsync(x => (long)x.CaloriesBurned)
        });
    }
}
