using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Reports;
using HealthTrack.Models.Training;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class AnalyticsService(ApplicationDbContext database, PlanAccess access, AppointmentClock clock)
{
    public async Task<ReportViewModel> BuildAsync(string? trainerId)
    {
        var today = DateOnly.FromDateTime(clock.Now);
        var month = new DateOnly(today.Year, today.Month, 1);
        var clients = trainerId is not null ? access.Clients(trainerId) : database.Users.Where(x =>
            database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Client)));
        var workouts = trainerId is not null ? access.Workouts(trainerId) : database.WorkoutPlans;
        var diets = trainerId is not null ? access.Diets(trainerId) : database.DietPlans;
        var workoutLogs = database.WorkoutLogs.Where(x => workouts.Any(p => p.Id == x.WorkoutPlanId) && clients.Any(c => c.Id == x.ClientId));
        var mealLogs = database.MealLogs.Where(x => diets.Any(p => p.Id == x.DietPlanId) && clients.Any(c => c.Id == x.ClientId));
        var appointments = trainerId is null ? database.Appointments.AsQueryable()
            : database.Appointments.Where(x => x.TrainerId == trainerId && clients.Any(c => c.Id == x.ClientId));
        var completed = await appointments.CountAsync(x => x.Status == AppointmentStatus.Completed);
        var cancelled = await appointments.CountAsync(x => x.Status == AppointmentStatus.Cancelled);
        var total = await appointments.CountAsync();
        var report = new ReportViewModel
        {
            Title = "Engagement and Popular Plans",
            Scope = (trainerId is null ? "System-wide. " : "Your currently assigned active clients and your plans/appointments only. ")
                + $"Active clients means enabled Client accounts; active plans must include today ({today:yyyy-MM-dd}, {clock.TimeZoneId}). Monthly logs run from the first of this month through today. Popularity groups exact plan names across all dates and counts distinct assigned clients. Activity counts are log entries, not unique sessions. Completion rate is completed / all appointments, including pending and cancelled. Most active ranks this month's workout plus meal logs."
        };
        report.Metrics.AddRange([
            new("Active Clients", (await clients.CountAsync(x => x.IsActive)).ToString()),
            new("Workout Logs This Month", (await workoutLogs.CountAsync(x => x.WorkoutDate >= month && x.WorkoutDate <= today)).ToString()),
            new("Meal Logs This Month", (await mealLogs.CountAsync(x => x.MealDate >= month && x.MealDate <= today)).ToString()),
            new("Completed Appointments", completed.ToString()), new("Cancelled Appointments", cancelled.ToString()),
            new("Appointment Completion Rate", total == 0 ? "0% (no appointments)" : (100m * completed / total).ToString("0.##") + "%"),
            new("Active Workout Plans", (await workouts.CountAsync(x => x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today)).ToString()),
            new("Active Diet Plans", (await diets.CountAsync(x => x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today)).ToString())
        ]);
        var workoutCounts = await workoutLogs.GroupBy(x => x.WorkoutPlan.PlanName).Select(g => new { Name = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Name, x => x.Count);
        var dietCounts = await mealLogs.GroupBy(x => x.DietPlan.PlanName).Select(g => new { Name = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Name, x => x.Count);
        var popularWorkouts = await workouts.GroupBy(x => x.PlanName).Select(g => new { Name = g.Key, Clients = g.Select(x => x.ClientId).Distinct().Count() }).OrderByDescending(x => x.Clients).ThenBy(x => x.Name).ToListAsync();
        var popularDiets = await diets.GroupBy(x => x.PlanName).Select(g => new { Name = g.Key, Clients = g.Select(x => x.ClientId).Distinct().Count() }).OrderByDescending(x => x.Clients).ThenBy(x => x.Name).ToListAsync();
        report.Tables.Add(new("Popular workout routines", ["Workout plan / routine", "Assigned clients", "Workout log count"],
            popularWorkouts.Select(x => new[] { x.Name, x.Clients.ToString(), workoutCounts.GetValueOrDefault(x.Name).ToString() }).ToList()));
        report.Tables.Add(new("Popular diet plans", ["Diet plan", "Assigned clients", "Meal log count"],
            popularDiets.Select(x => new[] { x.Name, x.Clients.ToString(), dietCounts.GetValueOrDefault(x.Name).ToString() }).ToList()));
        var active = await clients.Select(c => new
        {
            c.Id, c.FullName,
            Workouts = workoutLogs.Count(x => x.ClientId == c.Id && x.WorkoutDate >= month && x.WorkoutDate <= today),
            Meals = mealLogs.Count(x => x.ClientId == c.Id && x.MealDate >= month && x.MealDate <= today)
        }).Where(x => x.Workouts + x.Meals > 0).OrderByDescending(x => x.Workouts + x.Meals).ThenBy(x => x.FullName).ThenBy(x => x.Id).Take(10).ToListAsync();
        report.Tables.Add(new("Most Active Clients (this month)", ["Client", "Workout logs", "Meal logs", "Total activity"],
            active.Select(x => new[] { x.FullName, x.Workouts.ToString(), x.Meals.ToString(), (x.Workouts + x.Meals).ToString() }).ToList()));
        return report;
    }
}
