using System.Security.Claims;
using HealthTrack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = "Client,Trainer,HealthcareProfessional")]
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

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var item = await database.Notifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnread(int id)
    {
        var item = await database.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (item is null) return NotFound();
        item.IsRead = false;
        await database.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, ActionName("Delete")]
    public async Task<IActionResult> ConfirmDelete(int id)
    {
        var item = await database.Notifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        return item is null ? NotFound() : View("Confirm", new HealthTrack.Models.ConfirmationViewModel(
            "Delete notification", $"Permanently delete the notification: {item.Title}?", "Delete", id, "Index"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await database.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (item is null) return NotFound();
        database.Notifications.Remove(item);
        await database.SaveChangesAsync();
        TempData["Success"] = "Notification deleted.";
        return RedirectToAction(nameof(Index));
    }
}
