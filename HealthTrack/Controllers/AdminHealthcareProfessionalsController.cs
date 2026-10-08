using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminHealthcareProfessionalsController(ApplicationDbContext database, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index() => View(await database.HealthcareProfessionals.AsNoTracking()
        .Include(x => x.User).OrderBy(x => x.User.FullName).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var profile = await database.HealthcareProfessionals.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == id);
        return profile is null ? NotFound() : View(profile);
    }

    [HttpGet] public IActionResult Create() => View(new CreateHealthcareProfessionalForm());

    [HttpPost]
    public async Task<IActionResult> Create(CreateHealthcareProfessionalForm model)
    {
        if (ModelState.IsValid)
        {
            await using var transaction = await database.Database.BeginTransactionAsync();
            try
            {
                var user = new ApplicationUser { FullName = model.FullName.Trim(), Email = model.Email.Trim(), UserName = model.Email.Trim(), IsActive = true };
                var result = await users.CreateAsync(user, model.Password);
                if (result.Succeeded) result = await users.AddToRoleAsync(user, AppRoles.HealthcareProfessional);
                if (result.Succeeded)
                {
                    database.HealthcareProfessionals.Add(new HealthcareProfessional { UserId = user.Id, Specialty = model.Specialty.Trim() });
                    await database.SaveChangesAsync();
                    await transaction.CommitAsync();
                    TempData["Success"] = "Healthcare professional created.";
                    return RedirectToAction(nameof(Index));
                }
                AddErrors(result);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            { ModelState.AddModelError(nameof(model.Email), "An account with this email already exists."); }
        }
        model.Password = model.ConfirmPassword = string.Empty;
        ModelState.SetModelValue(nameof(model.Password), "", "");
        ModelState.SetModelValue(nameof(model.ConfirmPassword), "", "");
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var profile = await database.HealthcareProfessionals.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == id);
        if (profile is null) return NotFound();
        return View(new EditHealthcareProfessionalForm { FullName = profile.User.FullName, Email = profile.User.Email!, Specialty = profile.Specialty, IsActive = profile.User.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, EditHealthcareProfessionalForm model)
    {
        var profile = await database.HealthcareProfessionals.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == id);
        if (profile is null) return NotFound();
        if (!ModelState.IsValid) return View(model);
        // Keep the existing administrator protections even for an externally assigned mixed-role account.
        if (!model.IsActive && (id == users.GetUserId(User)
            || (await users.IsInRoleAsync(profile.User, AppRoles.Admin)
                && await database.Users.CountAsync(x => x.IsActive && database.UserRoles.Any(ur => ur.UserId == x.Id
                    && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin))) <= 1)))
        {
            ModelState.AddModelError(nameof(model.IsActive), "Your own account and the last active administrator must remain active.");
            return View(model);
        }
        var existing = await users.FindByEmailAsync(model.Email.Trim());
        if (existing is not null && existing.Id != id)
        { ModelState.AddModelError(nameof(model.Email), "An account with this email already exists."); return View(model); }
        await using var transaction = await database.Database.BeginTransactionAsync();
        profile.Specialty = model.Specialty.Trim();
        profile.User.FullName = model.FullName.Trim();
        if (!string.Equals(profile.User.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase)) profile.User.EmailConfirmed = false;
        profile.User.Email = profile.User.UserName = model.Email.Trim();
        profile.User.IsActive = model.IsActive;
        try
        {
            var result = await users.UpdateAsync(profile.User);
            if (result.Succeeded && !model.IsActive) result = await users.UpdateSecurityStampAsync(profile.User);
            if (!result.Succeeded) { AddErrors(result); return View(model); }
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { ModelState.AddModelError(nameof(model.Email), "An account with this email already exists."); return View(model); }
        TempData["Success"] = "Healthcare professional updated.";
        return RedirectToAction(nameof(Index));
    }

    private void AddErrors(IdentityResult result)
    { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); }
}
