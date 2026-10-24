using System.Security.Claims;
using System.Text.Json;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Memberships;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

public class MembershipPackagesController(ApplicationDbContext database, AppointmentClock clock) : Controller
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isClient = User.IsInRole(AppRoles.Client);

        if (isAdmin)
        {
            var allPackages = await database.MembershipPackages.AsNoTracking()
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.PriceLKR)
                .ToListAsync();
            return View("AdminIndex", allPackages);
        }

        // Client or public catalog view
        var activePackages = await database.MembershipPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PriceLKR)
            .ToListAsync();

        ClientSubscription? activeSub = null;
        if (isClient && !string.IsNullOrEmpty(UserId))
        {
            var today = DateOnly.FromDateTime(clock.Now);
            activeSub = await database.ClientSubscriptions.AsNoTracking()
                .Include(s => s.MembershipPackage)
                .Where(s => s.ClientId == UserId && s.Status == SubscriptionStatus.Active && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();
        }

        var catalog = new MembershipCatalogViewModel
        {
            ActiveSubscription = activeSub,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            IsClient = isClient,
            IsAdmin = isAdmin,
            Packages = activePackages.Select(p => new MembershipPackageViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PriceLKR = p.PriceLKR,
                DurationDays = p.DurationDays,
                Benefits = ParseBenefits(p.BenefitsJson),
                IsActive = p.IsActive,
                IsCurrentSubscription = activeSub?.MembershipPackageId == p.Id,
                CurrentSubscriptionEndDate = activeSub?.MembershipPackageId == p.Id ? activeSub.EndDate : null
            }).ToList()
        };

        return View("Catalog", catalog);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult Create() => View(new MembershipPackageForm());

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MembershipPackageForm model)
    {
        if (ModelState.IsValid)
        {
            var benefitsList = (model.BenefitsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .ToList();

            var package = new MembershipPackage
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim(),
                PriceLKR = model.PriceLKR,
                DurationDays = model.DurationDays,
                BenefitsJson = JsonSerializer.Serialize(benefitsList),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            database.MembershipPackages.Add(package);
            await database.SaveChangesAsync();

            TempData["Success"] = $"Package '{package.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var package = await database.MembershipPackages.FindAsync(id);
        if (package is null) return NotFound();

        var benefitsList = ParseBenefits(package.BenefitsJson);
        var form = new MembershipPackageForm
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            PriceLKR = package.PriceLKR,
            DurationDays = package.DurationDays,
            BenefitsText = string.Join(Environment.NewLine, benefitsList),
            IsActive = package.IsActive
        };

        return View(form);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MembershipPackageForm model)
    {
        if (id != model.Id) return BadRequest();

        var package = await database.MembershipPackages.FindAsync(id);
        if (package is null) return NotFound();

        if (ModelState.IsValid)
        {
            var benefitsList = (model.BenefitsText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .ToList();

            package.Name = model.Name.Trim();
            package.Description = model.Description.Trim();
            package.PriceLKR = model.PriceLKR;
            package.DurationDays = model.DurationDays;
            package.BenefitsJson = JsonSerializer.Serialize(benefitsList);
            package.IsActive = model.IsActive;

            await database.SaveChangesAsync();

            TempData["Success"] = $"Package '{package.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var package = await database.MembershipPackages.FindAsync(id);
        if (package is null) return NotFound();

        package.IsActive = !package.IsActive;
        await database.SaveChangesAsync();

        TempData["Success"] = $"Package '{package.Name}' {(package.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    private static List<string> ParseBenefits(string? benefitsJson)
    {
        if (string.IsNullOrWhiteSpace(benefitsJson)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(benefitsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
