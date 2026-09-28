using HealthTrack.Models;
using HealthTrack.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.ViewComponents;

public record NavigationLink(string Controller, string Action, string Label);
public record NavigationViewModel(bool IsAuthenticated, string? FullName, List<NavigationLink> Links,
    bool ShowNotifications, int UnreadNotifications);

public class NavigationViewComponent(UserManager<ApplicationUser> users, ApplicationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var principal = HttpContext.User;
        var authenticated = principal.Identity?.IsAuthenticated == true;
        var links = new List<NavigationLink>();
        void Add(string controller, string action, string label) => links.Add(new(controller, action, label));
        if (!authenticated) Add("Home", "Index", "Home");
        else
        {
            Add("Dashboard", "Index", "Dashboard");
            if (principal.IsInRole(AppRoles.Admin))
            {
                Add("Admin", "Clients", "Clients"); Add("Admin", "Appointments", "Appointments");
                Add("Admin", "Users", "Users"); Add("Progress", "Summary", "Health Summary");
                Add("Reports", "Index", "Reports");
            }
            else if (principal.IsInRole(AppRoles.Trainer))
            {
                Add("Clients", "Index", "Clients"); Add("TrainerWorkouts", "Index", "Workout Plans");
                Add("TrainerDiets", "Index", "Diet Plans"); Add("Appointments", "Index", "Appointments");
                Add("Reports", "Index", "Reports");
            }
            else if (principal.IsInRole(AppRoles.Client))
            {
                Add("Measurements", "Index", "Measurements"); Add("Progress", "Index", "Progress");
                Add("Workouts", "Index", "My Workouts"); Add("Workouts", "Log", "Log Workout");
                Add("Diets", "Index", "My Diet"); Add("Diets", "Log", "Log Meal");
                Add("Appointments", "Book", "Book Appointment"); Add("Appointments", "Index", "Appointments");
            }
        }
        var user = authenticated ? await users.GetUserAsync(principal) : null;
        var showNotifications = authenticated && (principal.IsInRole(AppRoles.Client) || principal.IsInRole(AppRoles.Trainer));
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = showNotifications
            ? await database.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead) : 0;
        return View(new NavigationViewModel(authenticated, user?.FullName ?? principal.Identity?.Name, links, showNotifications, unread));
    }
}
