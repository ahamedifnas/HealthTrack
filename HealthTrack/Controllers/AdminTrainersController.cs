
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[AutoValidateAntiforgeryToken]
public class AdminTrainersController(UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles, ApplicationDbContext database) : Controller
{
    public IActionResult Index() => RedirectToAction("Admin", "Dashboard");

    [HttpGet]
    public IActionResult Create() => View(new CreateTrainerViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateTrainerViewModel model)
    {
        if (!ModelState.IsValid) return CreateWithErrors(model);
        var email = model.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return CreateWithErrors(model);
        }

        // Identity hashes the password; account creation and role assignment succeed together.
        await using var transaction = await database.Database.BeginTransactionAsync();
        try
        {
            if (!await roles.RoleExistsAsync(AppRoles.Trainer))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole(AppRoles.Trainer));
                if (!roleResult.Succeeded)
                {
                    AddErrors(roleResult);
                    return CreateWithErrors(model);
                }
            }
            var trainer = new ApplicationUser
            {
                FullName = model.FullName.Trim(), UserName = email, Email = email, IsActive = true
            };
            var result = await users.CreateAsync(trainer, model.Password);
            if (result.Succeeded) result = await users.AddToRoleAsync(trainer, AppRoles.Trainer);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return CreateWithErrors(model);
            }
            await transaction.CommitAsync();
            TempData["Success"] = "Trainer account created. The trainer can now log in.";
            return RedirectToAction("Admin", "Dashboard");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request may have created the same username/email after validation.
            ModelState.AddModelError(string.Empty, "The account or role was created by another request. Check the email and try again.");
            return CreateWithErrors(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var trainer = await users.FindByIdAsync(id);
        if (trainer is null || !await users.IsInRoleAsync(trainer, AppRoles.Trainer)) return NotFound();
        return View(new EditTrainerViewModel { Id = trainer.Id, FullName = trainer.FullName, Email = trainer.Email ?? string.Empty });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, EditTrainerViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var trainer = await users.FindByIdAsync(id);
        if (trainer is null || !await users.IsInRoleAsync(trainer, AppRoles.Trainer)) return NotFound();
        model.Id = trainer.Id;
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && existing.Id != trainer.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }
        trainer.FullName = model.FullName.Trim();
        if (!string.Equals(trainer.Email, email, StringComparison.OrdinalIgnoreCase)) trainer.EmailConfirmed = false;
        trainer.Email = email;
        trainer.UserName = email;
        try
        {
            var result = await users.UpdateAsync(trainer);
            if (!result.Succeeded) { AddErrors(result); return View(model); }
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }
        TempData["Success"] = "Trainer details updated.";
        return RedirectToAction("Admin", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(string id)
    {
        var trainer = await users.FindByIdAsync(id);
        if (trainer is null || !await users.IsInRoleAsync(trainer, AppRoles.Trainer)) return NotFound();
        return View("Confirm", new ConfirmationViewModel("Deactivate trainer",
            $"Deactivate {trainer.FullName}? Their plans, appointments and client history will be preserved.", "Deactivate", id, "Admin", null, "Dashboard"));
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName("Deactivate")]
    public Task<IActionResult> DeactivateConfirmed(string id) => SetActive(id, false);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, [BindRequired] bool isActive)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var trainer = await users.FindByIdAsync(id);
        if (trainer is null || !await users.IsInRoleAsync(trainer, AppRoles.Trainer)) return NotFound();
        if (trainer.Id == users.GetUserId(User) && !isActive)
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction("Admin", "Dashboard");
        }
        trainer.IsActive = isActive;
        var result = isActive
            ? await users.UpdateAsync(trainer)
            : await users.UpdateSecurityStampAsync(trainer);
        if (result.Succeeded) TempData["Success"] = isActive ? "Trainer activated." : "Trainer deactivated.";
        else TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description));
        return RedirectToAction("Admin", "Dashboard");
    }

    private IActionResult CreateWithErrors(CreateTrainerViewModel model)
    {
        model.Password = model.ConfirmPassword = string.Empty;
        ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(model.ConfirmPassword), string.Empty, string.Empty);
        return View("Create", model);
    }
    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }
}
