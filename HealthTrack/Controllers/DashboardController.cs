using HealthTrack.Models;
using HealthTrack.Models.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class DashboardController(UserManager<ApplicationUser> users) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Admin)) return RedirectToAction(nameof(Admin));
        if (User.IsInRole(AppRoles.Trainer)) return RedirectToAction(nameof(Trainer));
        if (User.IsInRole(AppRoles.Client)) return RedirectToAction(nameof(Client));
        return Forbid();
    }

    [Authorize(Roles = AppRoles.Client)]
    public async Task<IActionResult> Client()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new ClientDashboardViewModel { FullName = user.FullName });
    }

    [Authorize(Roles = AppRoles.Trainer)]
    public IActionResult Trainer() => View();

    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Admin()
    {
        return View(new AdminDashboardViewModel
        {
            TotalUsers = await users.Users.CountAsync(),
            TotalClients = (await users.GetUsersInRoleAsync(AppRoles.Client)).Count,
            TotalTrainers = (await users.GetUsersInRoleAsync(AppRoles.Trainer)).Count,
            ActiveUsers = await users.Users.CountAsync(user => user.IsActive)
        });
    }
}
