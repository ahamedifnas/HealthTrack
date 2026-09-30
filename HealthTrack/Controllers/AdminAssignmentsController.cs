using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using HealthTrack.Models.Training;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[AutoValidateAntiforgeryToken]
public class AdminAssignmentsController(ApplicationDbContext database, AdminDashboardData dashboard) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Assign([Bind(Prefix = "Assignment")] AssignClientViewModel model)
    {
        if (!await dashboard.ActiveClients().AnyAsync(x => x.Id == model.ClientId))
            ModelState.AddModelError("Assignment.ClientId", "Select an active Client.");
        if (!await dashboard.ActiveTrainers().AnyAsync(x => x.Id == model.TrainerId))
            ModelState.AddModelError("Assignment.TrainerId", "Select an active Trainer.");
        if (!ModelState.IsValid) return await InvalidAssignment(model);
        var existing = await database.TrainerClients.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == model.ClientId);
        if (existing is not null)
        {
            if (existing.TrainerId == model.TrainerId)
            {
                TempData["Success"] = "This client is already assigned to this trainer.";
                return RedirectToAction("Admin", "Dashboard");
            }
            ModelState.AddModelError("Assignment.ClientId", "This client is already assigned to another trainer. The existing assignment was not changed.");
            return await InvalidAssignment(model);
        }
        database.TrainerClients.Add(new TrainerClient { ClientId = model.ClientId, TrainerId = model.TrainerId });
        try { await database.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError("Assignment.ClientId", "This client was assigned by another request. Refresh the dashboard to see the assignment.");
            return await InvalidAssignment(model);
        }
        TempData["Success"] = "Client assigned to trainer successfully.";
        return RedirectToAction("Admin", "Dashboard");
    }

    private async Task<IActionResult> InvalidAssignment(AssignClientViewModel assignment)
    {
        var model = await dashboard.LoadAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        model.Assignment = assignment;
        return View("~/Views/Dashboard/Admin.cshtml", model);
    }
}
