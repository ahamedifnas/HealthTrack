using HealthTrack.Models;
using HealthTrack.Data;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Training;
using HealthTrack.Services;
using System.Security.Claims;
using HealthTrack.Models.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class DashboardController(UserManager<ApplicationUser> users, ApplicationDbContext database, AppointmentClock clock, PlanAccess access, AdminDashboardData adminDashboard) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Admin)) return RedirectToAction(nameof(Admin));
        if (User.IsInRole(AppRoles.Trainer)) return RedirectToAction(nameof(Trainer));
        if (User.IsInRole(AppRoles.Client)) return RedirectToAction(nameof(Client));
        return Forbid();
    }

    [Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Client()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);
        var measurements = database.HealthMeasurements.AsNoTracking().Where(x => x.ClientId == user.Id);
        var latest = await measurements.OrderByDescending(x => x.MeasurementDate).FirstOrDefaultAsync();
        var since = today.AddDays(-6);
        var recentWorkoutCalories = await database.WorkoutLogs.AsNoTracking()
            .Where(x => x.ClientId == user.Id && x.WorkoutDate >= since && x.WorkoutDate <= today
                && x.CaloriesBurned > 0)
            .GroupBy(x => x.ClientId)
            .Select(logs => (long?)logs.Sum(x => (long)x.CaloriesBurned))
            .FirstOrDefaultAsync();
        return View(new ClientDashboardViewModel
        {
            FullName = user.FullName,
            StartingWeight = await measurements.Where(x => x.MeasurementDate <= today)
                .OrderBy(x => x.MeasurementDate).ThenBy(x => x.Id).Select(x => (decimal?)x.WeightKg).FirstOrDefaultAsync(),
            MeasurementCount = await measurements.CountAsync(),
            UnreadNotifications = await database.Notifications.CountAsync(x => x.UserId == user.Id && !x.IsRead),
            RecentNotifications = await database.Notifications.AsNoTracking().Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(3).ToListAsync(),
            // Preview the newest active plan whose dates include today.
            WorkoutPlan = await database.WorkoutPlans.AsNoTracking()
                .Where(x => x.ClientId == user.Id && x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today)
                .Include(x => x.Exercises.OrderBy(e => e.Id).Take(3))
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(),
            DietPlan = await database.DietPlans.AsNoTracking()
                .Where(x => x.ClientId == user.Id && x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today)
                .Include(x => x.Meals.OrderBy(m => m.MealType).ThenBy(m => m.Id).Take(4))
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(),
            CurrentWeight = latest?.WeightKg, BMI = latest?.BMI, TargetWeight = user.TargetWeight,
            LatestMeasurementDate = latest?.MeasurementDate,
            CaloriesBurned = recentWorkoutCalories,
            NextAppointment = await database.Appointments.AsNoTracking().Include(x => x.Trainer)
                .Where(x => x.ClientId == user.Id && x.Status == AppointmentStatus.Approved
                    && (x.AppointmentDate > today || (x.AppointmentDate == today && x.AppointmentTime > time)))
                .OrderBy(x => x.AppointmentDate).ThenBy(x => x.AppointmentTime).FirstOrDefaultAsync()
        });
    }

    [Authorize(Policy = FeaturePolicies.Trainer)]
    public async Task<IActionResult> Trainer()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);
        var assignedClients = access.Clients(userId);
        return View(new TrainerDashboardViewModel
        {
            FullName = user.FullName,
            UpcomingAppointments = await database.Appointments.AsNoTracking()
                .Where(x => x.TrainerId == userId && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved)
                    && (x.AppointmentDate > today || (x.AppointmentDate == today && x.AppointmentTime > time)))
                .OrderBy(x => x.AppointmentDate).ThenBy(x => x.AppointmentTime).ThenBy(x => x.Id).Take(5)
                .Select(x => new DashboardAppointment(x.Id, x.Client.FullName, x.Trainer.FullName,
                    x.AppointmentDate, x.AppointmentTime, x.Status)).ToListAsync(),
            TotalClients = await access.Clients(userId).CountAsync(),
            ActiveWorkoutPlans = await access.Workouts(userId).CountAsync(x => x.Status == PlanStatus.Active),
            ActiveDietPlans = await access.Diets(userId).CountAsync(x => x.Status == PlanStatus.Active),
            RecentClients = await database.TrainerClients.AsNoTracking()
                .Where(x => x.TrainerId == userId && assignedClients.Any(c => c.Id == x.ClientId))
                .OrderByDescending(x => x.AssignedAt).ThenBy(x => x.ClientId).Take(5)
                .Select(x => new RecentClientItem(x.ClientId, x.Client.FullName, x.Client.Email, x.AssignedAt)).ToListAsync(),
            TodayAppointmentCount = await database.Appointments.CountAsync(x => x.TrainerId == userId
                && x.AppointmentDate == today && x.Status != AppointmentStatus.Cancelled),
            PendingAppointmentCount = await database.Appointments.CountAsync(x => x.TrainerId == userId
                && x.Status == AppointmentStatus.Pending)
        });
    }

    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Admin(bool addTrainer = false)
    {
        var model = await adminDashboard.LoadAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (addTrainer) return RedirectToAction("Create", "AdminTrainers");
        return View(model);
    }
}
