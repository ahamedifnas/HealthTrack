using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(ApplicationDbContext database) : Controller
{
    public IActionResult Index() => RedirectToAction("Admin", "Dashboard");

    public async Task<IActionResult> Clients() => View(await database.Users.AsNoTracking()
        .Where(user => database.UserRoles.Any(ur => ur.UserId == user.Id &&
            database.Roles.Any(role => role.Id == ur.RoleId && role.Name == AppRoles.Client)))
        .OrderBy(user => user.FullName).ThenBy(user => user.Email)
        .Select(user => new AdminUserListItem(user.FullName, user.Email, user.IsActive, user.CreatedAt,
            new List<string>())).ToListAsync());

    public async Task<IActionResult> Users()
    {
        var memberships = await (from ur in database.UserRoles
            join role in database.Roles on ur.RoleId equals role.Id
            select new { ur.UserId, role.Name }).ToListAsync();
        var roles = memberships.ToLookup(x => x.UserId, x => x.Name!);
        var users = await database.Users.AsNoTracking().OrderBy(x => x.FullName).ThenBy(x => x.Email).ToListAsync();
        return View(users.Select(x => new AdminUserListItem(x.FullName, x.Email, x.IsActive,
            x.CreatedAt, roles[x.Id].OrderBy(role => role).ToList())).ToList());
    }

    public async Task<IActionResult> Appointments() => View(await database.Appointments.AsNoTracking()
        .Include(x => x.Client).Include(x => x.Trainer)
        .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime).ToListAsync());
}
