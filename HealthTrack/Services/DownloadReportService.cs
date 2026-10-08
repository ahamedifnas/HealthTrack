using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Reports;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class DownloadReportService(ApplicationDbContext database, ReportService reports, AnalyticsService analytics, AppointmentClock clock)
{
    private static string Number(decimal? value) => value?.ToString("0.##") ?? "Not recorded";
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd");

    public async Task<ReportViewModel> AdminAsync(AdminReportKind kind, ReportFilter filter, string? clientId)
    {
        if (kind == AdminReportKind.System) return await reports.AdminAsync();
        if (kind is AdminReportKind.WorkoutAnalytics or AdminReportKind.DietAnalytics or AdminReportKind.Engagement)
        {
            var source = await analytics.BuildAsync(null);
            var result = new ReportViewModel { Title = $"Admin {kind} Report", Scope = source.Scope };
            if (kind == AdminReportKind.Engagement) result.Metrics.AddRange(source.Metrics);
            var title = kind == AdminReportKind.WorkoutAnalytics ? "Popular workout routines" : kind == AdminReportKind.DietAnalytics ? "Popular diet plans" : "Most Active Clients (this month)";
            result.Tables.AddRange(source.Tables.Where(t => t.Title == title));
            if (kind != AdminReportKind.Engagement) result.Metrics.Add(new("Plan groups", result.Tables.Sum(t => t.Rows.Count).ToString()));
            return result;
        }
        if (kind == AdminReportKind.Appointments)
            return await AppointmentAsync(database.Appointments.Where(x => clientId == null || x.ClientId == clientId), filter, "Admin Appointment Report", null);

        var role = kind switch { AdminReportKind.Clients => AppRoles.Client, AdminReportKind.Trainers => AppRoles.Trainer,
            AdminReportKind.HealthcareProfessionals => AppRoles.HealthcareProfessional, _ => null };
        var accounts = database.Users.AsNoTracking().Where(x => role == null || database.UserRoles.Any(ur => ur.UserId == x.Id
            && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        var users = await accounts.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.FullName, x.Email, x.IsActive, x.CreatedAt }).ToListAsync();
        var specialties = kind == AdminReportKind.HealthcareProfessionals
            ? await database.HealthcareProfessionals.ToDictionaryAsync(x => x.UserId, x => x.Specialty) : [];
        var report = new ReportViewModel { Title = $"Admin {kind} Report", Scope = "All registered accounts in the selected category, including inactive accounts. Account creation times are UTC. Password/security fields are excluded." };
        report.Metrics.AddRange([new("Accounts", users.Count.ToString()), new("Active accounts", users.Count(x => x.IsActive).ToString())]);
        report.Tables.Add(new("Account directory", ["Full name", "Email", "Status", "Created (UTC)", "Category / specialty"],
            users.Select(x => new[] { x.FullName, x.Email ?? "-", x.IsActive ? "Active" : "Inactive", x.CreatedAt.ToString("yyyy-MM-dd"), specialties.GetValueOrDefault(x.Id) ?? role ?? "All users" }).ToList()));
        return report;
    }

    public async Task<ReportViewModel?> PersonalAsync(string userId, PersonalReportKind kind, ReportFilter filter)
    {
        var client = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (client is null) return null;
        if (kind == PersonalReportKind.Appointments)
            return await AppointmentAsync(database.Appointments.Where(x => x.ClientId == userId), filter, "Personal Appointment History", client.FullName);
        var report = new ReportViewModel { Title = kind switch { PersonalReportKind.Progress => "Personal Progress Report", PersonalReportKind.Measurements => "Health Measurement History", PersonalReportKind.Workout => "Workout History", _ => "Diet and Meal History" }, ClientName = client.FullName, Scope = "Your own recorded history only. Dates are inclusive; summary totals use the same date range as the table.", Filter = filter };
        if (kind is PersonalReportKind.Progress or PersonalReportKind.Measurements)
        {
            var items = await database.HealthMeasurements.AsNoTracking().Where(x => x.ClientId == userId
                && (!filter.FromDate.HasValue || x.MeasurementDate >= filter.FromDate) && (!filter.ToDate.HasValue || x.MeasurementDate <= filter.ToDate))
                .OrderBy(x => x.MeasurementDate).ThenBy(x => x.Id).ToListAsync();
            var last = items.LastOrDefault();
            report.Metrics.AddRange([new("Measurements in range", items.Count.ToString()), new("Latest weight in range (kg)", Number(last?.WeightKg)),
                new("Latest BMI in range", Number(last?.BMI)), new("Current target weight (kg)", Number(client.TargetWeight))]);
            report.Tables.Add(new("Health measurement history", ["Date", "Weight (kg)", "Height (cm)", "BMI", "Calories", "Notes"],
                items.Select(x => new[] { Date(x.MeasurementDate), Number(x.WeightKg), Number(x.HeightCm), Number(x.BMI), x.CaloriesBurned.ToString(), x.Notes ?? "-" }).ToList()));
            if (kind == PersonalReportKind.Progress)
            {
                var workouts = database.WorkoutLogs.Where(x => x.ClientId == userId && (!filter.FromDate.HasValue || x.WorkoutDate >= filter.FromDate) && (!filter.ToDate.HasValue || x.WorkoutDate <= filter.ToDate));
                var meals = database.MealLogs.Where(x => x.ClientId == userId && (!filter.FromDate.HasValue || x.MealDate >= filter.FromDate) && (!filter.ToDate.HasValue || x.MealDate <= filter.ToDate));
                report.Metrics.AddRange([new("Workout logs in range", (await workouts.CountAsync()).ToString()), new("Completed workouts in range", (await workouts.CountAsync(x => x.Completed)).ToString()), new("Meal logs in range", (await meals.CountAsync()).ToString())]);
            }
        }
        else if (kind == PersonalReportKind.Workout)
        {
            var items = await database.WorkoutLogs.AsNoTracking().Include(x => x.WorkoutPlan).Where(x => x.ClientId == userId
                && (!filter.FromDate.HasValue || x.WorkoutDate >= filter.FromDate) && (!filter.ToDate.HasValue || x.WorkoutDate <= filter.ToDate))
                .OrderByDescending(x => x.WorkoutDate).ThenBy(x => x.Id).ToListAsync();
            report.Metrics.AddRange([new("Workout logs", items.Count.ToString()), new("Completed logs", items.Count(x => x.Completed).ToString()), new("Calories burned", items.Sum(x => (long)x.CaloriesBurned).ToString())]);
            report.Tables.Add(new("Workout history", ["Date", "Plan", "Minutes", "Calories", "Completed", "Notes"], items.Select(x => new[] { Date(x.WorkoutDate), x.WorkoutPlan.PlanName, x.DurationMinutes.ToString(), x.CaloriesBurned.ToString(), x.Completed ? "Yes" : "No", x.Notes ?? "-" }).ToList()));
        }
        else
        {
            var items = await database.MealLogs.AsNoTracking().Include(x => x.DietPlan).Where(x => x.ClientId == userId
                && (!filter.FromDate.HasValue || x.MealDate >= filter.FromDate) && (!filter.ToDate.HasValue || x.MealDate <= filter.ToDate))
                .OrderByDescending(x => x.MealDate).ThenBy(x => x.Id).ToListAsync();
            report.Metrics.AddRange([new("Meal logs", items.Count.ToString()), new("Recorded calories", items.Sum(x => (long)x.Calories).ToString())]);
            report.Tables.Add(new("Meal history", ["Date", "Plan", "Meal", "Food", "Quantity", "Calories", "Notes"], items.Select(x => new[] { Date(x.MealDate), x.DietPlan.PlanName, x.MealType.ToString(), x.FoodName, x.Quantity, x.Calories.ToString(), x.Notes ?? "-" }).ToList()));
        }
        return report;
    }

    public async Task<ReportViewModel> AppointmentAsync(IQueryable<Appointment> query, ReportFilter filter, string title, string? name)
    {
        var items = await query.AsNoTracking().Where(x => (!filter.FromDate.HasValue || x.AppointmentDate >= filter.FromDate)
            && (!filter.ToDate.HasValue || x.AppointmentDate <= filter.ToDate) && (!filter.Status.HasValue || x.Status == filter.Status))
            .Include(x => x.Client).Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
            .OrderBy(x => x.AppointmentDate).ThenBy(x => x.AppointmentTime).ThenBy(x => x.Id).ToListAsync();
        var report = new ReportViewModel { Title = title, ClientName = name, Filter = filter, Scope = $"Appointment times use {clock.TimeZoneId}. Only permitted appointments matching the selected filters are included." };
        report.Metrics.Add(new("Appointments in range", items.Count.ToString()));
        foreach (var status in Enum.GetValues<AppointmentStatus>()) report.Metrics.Add(new(status.ToString(), items.Count(x => x.Status == status).ToString()));
        report.Tables.Add(new("Appointment history", ["Date / time", "Client", "Provider", "Status", "Reason", "Notes"], items.Select(x => new[] { $"{x.AppointmentDate:yyyy-MM-dd} {x.AppointmentTime:HH:mm}", x.Client.FullName, x.ProviderName, x.Status.ToString(), x.Reason, x.Notes ?? "-" }).ToList()));
        return report;
    }
}
