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
using System.Data;
namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[AutoValidateAntiforgeryToken]
public class AdminAssignmentsController(ApplicationDbContext database, AdminDashboardData dashboard) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var assignment = await database.TrainerClients.AsNoTracking().Include(x => x.Client).Include(x => x.Trainer)
            .SingleOrDefaultAsync(x => x.ClientId == id);
        return assignment is null ? NotFound() : View(assignment);
    }

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

    [HttpGet]
    public async Task<IActionResult> Reassign(string id)
    {
        var assignment = await database.TrainerClients.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == id);
        if (assignment is null) return NotFound();
        await PrepareReassignment(id, assignment.TrainerId);
        return View(new AssignClientViewModel { ClientId = id, TrainerId = assignment.TrainerId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reassign(string id, AssignClientViewModel model,
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string expectedTrainerId)
    {
        if (model.ClientId != id) return BadRequest();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assignment = await database.TrainerClients.SingleOrDefaultAsync(x => x.ClientId == id);
        if (assignment is null) return NotFound();
        if (assignment.TrainerId != expectedTrainerId)
            ModelState.AddModelError(string.Empty, "This assignment changed. Reload the page before reassigning it.");
        if (!await dashboard.ActiveClients().AnyAsync(x => x.Id == id))
            ModelState.AddModelError(nameof(model.ClientId), "Only active clients can be reassigned.");
        if (!await dashboard.ActiveTrainers().AnyAsync(x => x.Id == model.TrainerId))
            ModelState.AddModelError(nameof(model.TrainerId), "Select an active trainer.");
        if (!ModelState.IsValid)
        {
            await PrepareReassignment(id, assignment.TrainerId);
            return View(model);
        }
        if (assignment.TrainerId != model.TrainerId)
        {
            // The composite key cannot be edited. Replace the current assignment atomically.
            // Historical plans and logs keep their original trainer and client references.
            database.TrainerClients.Remove(assignment);
            await database.SaveChangesAsync();
            database.TrainerClients.Add(new TrainerClient { ClientId = id, TrainerId = model.TrainerId });
            await database.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        TempData["Success"] = "Client assignment updated. Existing plans and history have been preserved.";
        return RedirectToAction("Admin", "Dashboard");
    }

    [HttpGet, ActionName("Unassign")]
    public async Task<IActionResult> ConfirmUnassign(string id)
    {
        var assignment = await database.TrainerClients.AsNoTracking().Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.ClientId == id);
        return assignment is null ? NotFound() : View("Confirm", new ConfirmationViewModel(
            "Unassign client", $"Unassign {assignment.Client.FullName}? Existing plans and logs will be preserved, and trainer access through this assignment will end.",
            "Unassign", id, "Admin", null, "Dashboard"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(string id)
    {
        var assignment = await database.TrainerClients.SingleOrDefaultAsync(x => x.ClientId == id);
        if (assignment is null) return NotFound();
        database.TrainerClients.Remove(assignment);
        await database.SaveChangesAsync();
        TempData["Success"] = "Client unassigned. Existing plans and history have been preserved.";
        return RedirectToAction("Admin", "Dashboard");
    }

    private async Task PrepareReassignment(string clientId, string trainerId)
    {
        ViewData["ClientName"] = await database.Users.Where(x => x.Id == clientId).Select(x => x.FullName).SingleAsync();
        ViewData["ExpectedTrainerId"] = trainerId;
        ViewData["Trainers"] = await dashboard.ActiveTrainers().OrderBy(x => x.FullName)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.FullName, x.Id)).ToListAsync();
    }
}
