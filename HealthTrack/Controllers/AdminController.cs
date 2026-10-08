using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using System.Data;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(ApplicationDbContext database, UserManager<ApplicationUser> users) : Controller
{
    public IActionResult Index() => RedirectToAction("Admin", "Dashboard");

    public async Task<IActionResult> Clients() => View(await database.Users.AsNoTracking()
        .Where(user => database.UserRoles.Any(ur => ur.UserId == user.Id &&
            database.Roles.Any(role => role.Id == ur.RoleId && role.Name == AppRoles.Client)))
        .OrderBy(user => user.FullName).ThenBy(user => user.Email)
        .Select(user => new AdminUserListItem(user.FullName, user.Email, user.IsActive, user.CreatedAt,
            new List<string>(), user.Id, user.TargetWeight)).ToListAsync());

    public async Task<IActionResult> Users()
    {
        var memberships = await (from ur in database.UserRoles
            join role in database.Roles on ur.RoleId equals role.Id
            select new { ur.UserId, role.Name }).ToListAsync();
        var roles = memberships.ToLookup(x => x.UserId, x => x.Name!);
        var users = await database.Users.AsNoTracking().OrderBy(x => x.FullName).ThenBy(x => x.Email).ToListAsync();
        return View(users.Select(x => new AdminUserListItem(x.FullName, x.Email, x.IsActive,
            x.CreatedAt, roles[x.Id].OrderBy(role => role).ToList(), x.Id, x.TargetWeight)).ToList());
    }

    public async Task<IActionResult> Appointments() => View(await database.Appointments.AsNoTracking()
        .Include(x => x.Client).Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
        .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime).ToListAsync());

    [HttpGet]
    public IActionResult CreateClient() => View(new CreateTrainerViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClient(CreateTrainerViewModel model)
    {
        if (ModelState.IsValid && await users.FindByEmailAsync(model.Email.Trim()) is not null)
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
        if (ModelState.IsValid)
        {
            await using var transaction = await database.Database.BeginTransactionAsync();
            try
            {
                var client = new ApplicationUser { FullName = model.FullName.Trim(), Email = model.Email.Trim(), UserName = model.Email.Trim() };
                var result = await users.CreateAsync(client, model.Password);
                if (result.Succeeded) result = await users.AddToRoleAsync(client, AppRoles.Client);
                if (result.Succeeded)
                {
                    await transaction.CommitAsync();
                    TempData["Success"] = "Client account created.";
                    return RedirectToAction(nameof(Details), new { id = client.Id });
                }
                AddErrors(result);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            { ModelState.AddModelError(nameof(model.Email), "An account with this email already exists."); }
        }
        model.Password = model.ConfirmPassword = string.Empty;
        ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(model.ConfirmPassword), string.Empty, string.Empty);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        ViewData["HasHistory"] = await HasHistory(id);
        ViewData["CanRemove"] = await CanDisable(user);
        return View(new AdminUserListItem(user.FullName, user.Email, user.IsActive, user.CreatedAt,
            (await users.GetRolesAsync(user)).ToList(), user.Id, user.TargetWeight));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new EditTrainerViewModel { Id = user.Id, FullName = user.FullName, Email = user.Email ?? "", IsActive = user.IsActive, TargetWeight = user.TargetWeight });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditTrainerViewModel model)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        model.Id = user.Id;
        if (!model.IsActive && !await CanDisable(user))
            ModelState.AddModelError(nameof(model.IsActive), "Your own account and the last active administrator must remain active.");
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim();
        var duplicate = await users.FindByEmailAsync(email);
        if (duplicate is not null && duplicate.Id != id) ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
        if (!ModelState.IsValid) return View(model);
        var changedEmail = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        user.FullName = model.FullName.Trim(); user.Email = user.UserName = email;
        if (changedEmail) user.EmailConfirmed = false;
        user.IsActive = model.IsActive;
        user.TargetWeight = model.TargetWeight is { } target ? decimal.Round(target, 2, MidpointRounding.AwayFromZero) : null;
        try
        {
            var result = await users.UpdateAsync(user);
            if (result.Succeeded && (changedEmail || !user.IsActive)) result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) { AddErrors(result); return View(model); }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { ModelState.AddModelError(nameof(model.Email), "An account with this email already exists."); return View(model); }
        TempData["Success"] = "User details updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet, ActionName("Delete")]
    public async Task<IActionResult> ConfirmDelete(string id)
    {
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await CanDisable(user)) return Forbid();
        var history = await HasHistory(id);
        return View("Confirm", new ConfirmationViewModel(history ? "Deactivate user" : "Delete user",
            history ? $"Deactivate {user.FullName}? Their HealthTrack history will be preserved." : $"Permanently delete {user.FullName}? This account has no dependent HealthTrack records.",
            "Delete", id, "Details", id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await CanDisable(user)) return Forbid();
        var history = await HasHistory(id);
        IdentityResult result;
        if (history) { user.IsActive = false; result = await users.UpdateSecurityStampAsync(user); }
        else result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Details), new { id });
        }
        await transaction.CommitAsync();
        TempData["Success"] = history ? "User deactivated. All related records have been preserved." : "User deleted.";
        return RedirectToAction(nameof(Users));
    }

    private async Task<bool> CanDisable(ApplicationUser user)
    {
        if (user.Id == users.GetUserId(User)) return false;
        if (!user.IsActive || !await users.IsInRoleAsync(user, AppRoles.Admin)) return true;
        return await database.Users.CountAsync(x => x.IsActive && database.UserRoles.Any(ur => ur.UserId == x.Id
            && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin))) > 1;
    }

    private async Task<bool> HasHistory(string id) =>
        await database.HealthMeasurements.AnyAsync(x => x.ClientId == id)
        || await database.TrainerClients.AnyAsync(x => x.ClientId == id || x.TrainerId == id)
        || await database.WorkoutPlans.AnyAsync(x => x.ClientId == id || x.TrainerId == id)
        || await database.DietPlans.AnyAsync(x => x.ClientId == id || x.TrainerId == id)
        || await database.WorkoutLogs.AnyAsync(x => x.ClientId == id)
        || await database.MealLogs.AnyAsync(x => x.ClientId == id)
        || await database.HealthcareProfessionals.AnyAsync(x => x.UserId == id)
        || await database.Appointments.AnyAsync(x => x.ClientId == id || x.TrainerId == id || x.HealthcareProfessionalId == id)
        || await database.Notifications.AnyAsync(x => x.UserId == id);

    private void AddErrors(IdentityResult result)
    { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); }
}
