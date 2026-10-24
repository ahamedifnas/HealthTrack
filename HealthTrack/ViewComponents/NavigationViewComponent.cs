using HealthTrack.Models;
using HealthTrack.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.ViewComponents;

public record NavigationLink(string Controller, string Action, string Label, string Icon = "circle");
public record NavigationViewModel(bool IsAuthenticated, string? FullName, List<NavigationLink> Links,
    bool ShowNotifications, int UnreadNotifications, string Role = "Member");

public class NavigationViewComponent(UserManager<ApplicationUser> users, ApplicationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var principal = HttpContext.User;
        var authenticated = principal.Identity?.IsAuthenticated == true;
        var links = new List<NavigationLink>();
        void Add(string controller, string action, string label, string icon = "circle") => links.Add(new(controller, action, label, icon));

        string roleName = "Guest";

        if (!authenticated)
        {
            Add("Home", "Index", "Home", "home");
            Add("MembershipPackages", "Index", "Membership Plans", "award");
        }
        else
        {
            Add("Dashboard", "Index", "Dashboard", "grid");
            if (principal.IsInRole(AppRoles.Admin))
            {
                roleName = "Admin";
                Add("Admin", "Clients", "Members", "users");
                Add("AdminTrainers", "Index", "Trainers", "user-check");
                Add("Admin", "Appointments", "Appointments", "calendar");
                Add("MembershipPackages", "Index", "Memberships", "award");
                Add("AdminPayments", "Index", "Payments", "credit-card");
                Add("Reports", "Index", "Reports", "bar-chart");
                Add("Notifications", "Index", "Notifications", "bell");
                Add("Admin", "Users", "Settings", "settings");
                Add("TrainerAvailability", "Index", "Trainer Schedules", "clock");
                Add("AdminHealthcareProfessionals", "Index", "Healthcare", "activity");
                Add("Progress", "Summary", "Health Summary", "trending-up");
            }
            else if (principal.IsInRole(AppRoles.Trainer))
            {
                roleName = "Trainer";
                Add("Clients", "Index", "Assigned Clients", "users");
                Add("TrainerWorkouts", "Index", "Workout Plans", "activity");
                Add("TrainerDiets", "Index", "Diet Plans", "coffee");
                Add("Appointments", "Index", "Appointments", "calendar");
                Add("TrainerAvailability", "Index", "My Availability", "clock");
                Add("Reports", "Index", "Reports", "bar-chart");
                Add("Notifications", "Index", "Notifications", "bell");
            }
            else if (principal.IsInRole(AppRoles.HealthcareProfessional))
            {
                roleName = "Healthcare";
                Add("Appointments", "Index", "Appointments", "calendar");
                Add("Notifications", "Index", "Notifications", "bell");
            }
            else if (principal.IsInRole(AppRoles.Client))
            {
                roleName = "Client";
                Add("Payments", "Membership", "My Membership", "award");
                Add("MembershipPackages", "Index", "Plans", "star");
                Add("Payments", "History", "My Payments", "credit-card");
                Add("Appointments", "Book", "Book Appointment", "calendar-plus");
                Add("Appointments", "Index", "Appointments", "calendar");
                Add("Workouts", "Index", "My Workouts", "activity");
                Add("Workouts", "Log", "Log Workout", "plus-circle");
                Add("Diets", "Index", "My Diet", "coffee");
                Add("Diets", "Log", "Log Meal", "plus-circle");
                Add("Measurements", "Index", "Measurements", "sliders");
                Add("Progress", "Index", "Progress", "trending-up");
                Add("Notifications", "Index", "Notifications", "bell");
            }
        }

        var user = authenticated ? await users.GetUserAsync(principal) : null;
        var showNotifications = authenticated;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = showNotifications && !string.IsNullOrEmpty(userId)
            ? await database.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead) : 0;

        return View(new NavigationViewModel(authenticated, user?.FullName ?? principal.Identity?.Name, links, showNotifications, unread, roleName));
    }
}
