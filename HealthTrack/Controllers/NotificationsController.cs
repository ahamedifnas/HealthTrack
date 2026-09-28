using System.Security.Claims;
using HealthTrack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = "Client,Trainer")]
[AutoValidateAntiforgeryToken]
public class NotificationsController(ApplicationDbContext database) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await database.Notifications.AsNoTracking()
        .Where(x => x.UserId == UserId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var notification = await database.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await database.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
