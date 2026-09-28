using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Reports;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class ReportService(ApplicationDbContext database, PlanAccess access, AppointmentClock clock)
{
    private static string Number(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Not recorded";
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public Task<List<ReportClient>> ClientsAsync(string trainerId) => access.Clients(trainerId).AsNoTracking()
        .OrderBy(x => x.FullName).ThenBy(x => x.Id)
        .Select(x => new ReportClient(x.Id, x.FullName, x.Email)).ToListAsync();

    public async Task<ReportViewModel?> ClientAsync(string trainerId, string clientId, ClientReportKind kind)
    {
        // Every report rechecks the live assignment; a dropdown is not an authorization boundary.
        var client = await access.Clients(trainerId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId);
        if (client is null) return null;
        var report = new ReportViewModel
        {
            Title = kind == ClientReportKind.Progress ? "Client Progress Report" : $"{kind} Report",
            ClientName = client.FullName,
            Scope = "All recorded dates. Plans, workout/meal logs and appointments are limited to you and this assigned client. Health measurements cover this client's recorded history."
        };
        var workouts = access.Workouts(trainerId).AsNoTracking().Where(x => x.ClientId == clientId);
        var diets = access.Diets(trainerId).AsNoTracking().Where(x => x.ClientId == clientId);
        var logs = database.WorkoutLogs.AsNoTracking().Where(x => x.ClientId == clientId && workouts.Any(p => p.Id == x.WorkoutPlanId));
        if (kind == ClientReportKind.Progress)
        {
            var measurements = await database.HealthMeasurements.AsNoTracking().Where(x => x.ClientId == clientId)
                .OrderBy(x => x.MeasurementDate).ThenBy(x => x.Id).ToListAsync();
            var latest = measurements.LastOrDefault();
            var first = measurements.FirstOrDefault();
            report.Metrics.AddRange([
                new("Current weight (kg)", Number(latest?.WeightKg)), new("Target weight (kg)", Number(client.TargetWeight)),
                new("Latest BMI", Number(latest?.BMI)), new("Measurements", measurements.Count.ToString()),
                new("First weight (kg)", Number(first?.WeightKg)),
                new("Weight change, latest minus first (kg)", Number(latest is null || first is null ? null : latest.WeightKg - first.WeightKg)),
                new("Workout logs", (await logs.CountAsync()).ToString()),
                new("Completed workout logs", (await logs.CountAsync(x => x.Completed)).ToString()),
                new("Incomplete workout logs", (await logs.CountAsync(x => !x.Completed)).ToString()),
                new("Workout calories burned (kcal)", (await logs.SumAsync(x => (long)x.CaloriesBurned)).ToString()),
                new("Measurement calories burned (kcal)", measurements.Sum(x => (long)x.CaloriesBurned).ToString())
            ]);
            report.Tables.Add(new("Weight history (calorie sources are separate; totals are not added together)",
                ["Date", "Weight (kg)", "BMI", "Measurement calories (kcal)"],
                measurements.Select(x => new[] { Date(x.MeasurementDate), Number(x.WeightKg), Number(x.BMI), x.CaloriesBurned.ToString() }).ToList()));
        }
        else if (kind == ClientReportKind.Workout)
        {
            var plans = await workouts.Include(x => x.Exercises).OrderBy(x => x.StartDate).ThenBy(x => x.Id).ToListAsync();
            var entries = await logs.Include(x => x.WorkoutPlan).Include(x => x.WorkoutExercise)
                .OrderByDescending(x => x.WorkoutDate).ThenByDescending(x => x.Id).ToListAsync();
            report.Metrics.AddRange([new("Workout plans", plans.Count.ToString()), new("Completed workout logs", entries.Count(x => x.Completed).ToString()),
                new("Calories burned (kcal)", entries.Sum(x => (long)x.CaloriesBurned).ToString())]);
            report.Tables.Add(new("Workout plans", ["Plan", "Status", "Start", "End", "Description"],
                plans.Select(x => new[] { x.PlanName, x.Status.ToString(), Date(x.StartDate), Date(x.EndDate), x.Description ?? "—" }).ToList()));
            report.Tables.Add(new("Exercises", ["Plan", "Exercise", "Sets", "Repetitions", "Minutes", "Target kcal", "Instructions"],
                plans.SelectMany(p => p.Exercises.OrderBy(x => x.Id).Select(x => new[] { p.PlanName, x.ExerciseName, x.Sets.ToString(), x.Repetitions.ToString(), x.DurationMinutes.ToString(), x.CaloriesTarget.ToString(), x.Instructions ?? "—" })).ToList()));
            report.Tables.Add(new("Workout logs", ["Plan", "Exercise", "Workout date", "Completed", "Completion date", "Minutes", "Burned kcal", "Notes"],
                entries.Select(x => new[] { x.WorkoutPlan.PlanName, x.WorkoutExercise?.ExerciseName ?? "Unspecified / deleted", Date(x.WorkoutDate), x.Completed ? "Yes" : "No", x.Completed ? Date(x.WorkoutDate) : "—", x.DurationMinutes.ToString(), x.CaloriesBurned.ToString(), x.Notes ?? "—" }).ToList()));
        }
        else if (kind == ClientReportKind.Diet)
        {
            var plans = await diets.Include(x => x.Meals).OrderBy(x => x.StartDate).ThenBy(x => x.Id).ToListAsync();
            var entries = await database.MealLogs.AsNoTracking().Where(x => x.ClientId == clientId && diets.Any(p => p.Id == x.DietPlanId))
                .Include(x => x.DietPlan).OrderByDescending(x => x.MealDate).ThenByDescending(x => x.Id).ToListAsync();
            report.Metrics.AddRange([new("Diet plans", plans.Count.ToString()), new("Meal logs", entries.Count.ToString()), new("Logged calories (kcal)", entries.Sum(x => (long)x.Calories).ToString())]);
            report.Tables.Add(new("Diet plans", ["Plan", "Status", "Start", "End", "Daily kcal", "Description"],
                plans.Select(x => new[] { x.PlanName, x.Status.ToString(), Date(x.StartDate), Date(x.EndDate), x.DailyCalories.ToString(), x.Description ?? "—" }).ToList()));
            report.Tables.Add(new("Meal items", ["Plan", "Meal", "Food", "Quantity", "kcal", "Protein (g)", "Carbs (g)", "Fat (g)", "Instructions"],
                plans.SelectMany(p => p.Meals.OrderBy(x => x.MealType).ThenBy(x => x.Id).Select(x => new[] { p.PlanName, x.MealType.ToString(), x.FoodName, x.Quantity, x.Calories.ToString(), Number(x.Protein), Number(x.Carbohydrates), Number(x.Fat), x.Instructions ?? "—" })).ToList()));
            report.Tables.Add(new("Meal logs", ["Plan", "Date", "Meal", "Food", "Quantity", "kcal", "Notes"],
                entries.Select(x => new[] { x.DietPlan.PlanName, Date(x.MealDate), x.MealType.ToString(), x.FoodName, x.Quantity, x.Calories.ToString(), x.Notes ?? "—" }).ToList()));
        }
        else
        {
            var appointments = await database.Appointments.AsNoTracking().Where(x => x.ClientId == clientId && x.TrainerId == trainerId)
                .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime).ThenByDescending(x => x.Id).ToListAsync();
            foreach (var status in Enum.GetValues<AppointmentStatus>())
            {
                var matches = appointments.Where(x => x.Status == status).ToList();
                report.Metrics.Add(new(status.ToString(), matches.Count.ToString()));
                report.Tables.Add(new($"{status} appointments ({clock.TimeZoneId})", ["Date", "Time", "Reason", "Notes"],
                    matches.Select(x => new[] { Date(x.AppointmentDate), x.AppointmentTime.ToString("HH:mm", CultureInfo.InvariantCulture), x.Reason, x.Notes ?? "—" }).ToList()));
            }
        }
        return report;
    }

    public async Task<ReportViewModel> AdminAsync()
    {
        var report = new ReportViewModel { Title = "Admin Report", IsAdminReport = true, Scope = "System-wide totals across all recorded dates, including inactive accounts and plans. Role totals count role membership; an account with multiple roles is counted in each applicable role." };
        async Task<int> RoleCount(string role) => await database.Users.CountAsync(x => database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        report.Metrics.AddRange([
            new("Total Users", (await database.Users.CountAsync()).ToString()),
            new("Total Clients", (await RoleCount(AppRoles.Client)).ToString()),
            new("Total Trainers", (await RoleCount(AppRoles.Trainer)).ToString()),
            new("Active Users", (await database.Users.CountAsync(x => x.IsActive)).ToString()),
            new("Total Appointments", (await database.Appointments.CountAsync()).ToString()),
            new("Total Workout Plans", (await database.WorkoutPlans.CountAsync()).ToString()),
            new("Total Diet Plans", (await database.DietPlans.CountAsync()).ToString())
        ]);
        var counts = await database.Appointments.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync();
        report.PrintMetrics.AddRange(report.Metrics);
        report.PrintMetrics.AddRange(Enum.GetValues<AppointmentStatus>().Select(status =>
            new ReportMetric($"{status} Appointments", (counts.SingleOrDefault(x => x.Status == status)?.Count ?? 0).ToString())));
        report.Tables.Add(new("Appointment status counts", ["Status", "Count"], Enum.GetValues<AppointmentStatus>()
            .Select(status => new[] { status.ToString(), (counts.SingleOrDefault(x => x.Status == status)?.Count ?? 0).ToString() }).ToList()));
        return report;
    }
}
