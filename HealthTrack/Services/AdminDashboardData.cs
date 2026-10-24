using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Dashboard;
using HealthTrack.Models.Memberships;
using HealthTrack.Models.Payments;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class AdminDashboardData(ApplicationDbContext database, UserManager<ApplicationUser> users, AppointmentClock clock)
{
    private IQueryable<ApplicationUser> ActiveRole(string role) => database.Users.AsNoTracking().Where(x => x.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == role))
        && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin)));

    public IQueryable<ApplicationUser> ActiveClients() => ActiveRole(AppRoles.Client).Where(x =>
        !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && (r.Name == AppRoles.Trainer || r.Name == AppRoles.HealthcareProfessional))));
    public IQueryable<ApplicationUser> ActiveTrainers() => ActiveRole(AppRoles.Trainer);

    public async Task<AdminDashboardViewModel> LoadAsync(string? adminId = null)
    {
        var trainers = await users.GetUsersInRoleAsync(AppRoles.Trainer);
        var today = DateOnly.FromDateTime(clock.Now);
        var now = clock.Now;
        var thirtyDaysAgo = now.AddDays(-30);

        var totalRevenue = await database.PaymentTransactions.AsNoTracking()
            .Where(x => !x.IsDemo && x.Status == PaymentStatus.Paid)
            .SumAsync(x => (decimal?)x.AmountLKR) ?? 0m;

        var newSignups = await database.Users.AsNoTracking()
            .CountAsync(x => x.CreatedAt >= thirtyDaysAgo);

        var totalPackages = await database.MembershipPackages.AsNoTracking().CountAsync();
        var activeSubs = await database.ClientSubscriptions.AsNoTracking()
            .CountAsync(x => x.Status == SubscriptionStatus.Active && x.EndDate >= today);

        // Chart trends for last 6 months
        var chartLabels = new List<string>();
        var chartSignups = new List<int>();
        var chartBookings = new List<int>();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = now.AddMonths(-i);
            var startOfMonth = new DateTime(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1);

            chartLabels.Add(targetMonth.ToString("MMM"));

            var signupsCount = await database.Users.AsNoTracking()
                .CountAsync(u => u.CreatedAt >= startOfMonth && u.CreatedAt < endOfMonth);
            chartSignups.Add(signupsCount);

            var dateOnlyStart = DateOnly.FromDateTime(startOfMonth);
            var dateOnlyEnd = DateOnly.FromDateTime(endOfMonth);
            var bookingsCount = await database.Appointments.AsNoTracking()
                .CountAsync(a => a.AppointmentDate >= dateOnlyStart && a.AppointmentDate < dateOnlyEnd);
            chartBookings.Add(bookingsCount);
        }

        // Build Recent Member Activity list
        var activities = new List<MemberActivityItem>();

        var recentApptsWithDetails = await database.Appointments.AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Trainer)
            .Include(a => a.HealthcareProfessional).ThenInclude(hp => hp!.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync();

        foreach (var appt in recentApptsWithDetails)
        {
            var trainerName = appt.Trainer?.FullName ?? appt.HealthcareProfessional?.User?.FullName ?? "Staff";
            var statusStr = appt.Status switch
            {
                AppointmentStatus.Approved => "Approved",
                AppointmentStatus.Completed => "Completed",
                AppointmentStatus.Pending => "Pending",
                AppointmentStatus.Cancelled => "Cancelled",
                _ => appt.Status.ToString()
            };
            activities.Add(new MemberActivityItem
            {
                MemberName = appt.Client.FullName,
                MemberEmail = appt.Client.Email ?? string.Empty,
                Activity = "Personal Training",
                MembershipType = "Standard Tier",
                TrainerName = trainerName,
                Timestamp = appt.CreatedAt,
                Status = statusStr,
                StatusType = (appt.Status == AppointmentStatus.Approved || appt.Status == AppointmentStatus.Completed) ? "green" : "purple"
            });
        }

        var recentSubs = await database.ClientSubscriptions.AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.MembershipPackage)
            .OrderByDescending(s => s.ActivatedAt)
            .Take(3)
            .ToListAsync();

        foreach (var sub in recentSubs)
        {
            activities.Add(new MemberActivityItem
            {
                MemberName = sub.Client.FullName,
                MemberEmail = sub.Client.Email ?? string.Empty,
                Activity = "Membership Enrolled",
                MembershipType = !string.IsNullOrWhiteSpace(sub.PackageNameSnapshot) ? sub.PackageNameSnapshot : (sub.MembershipPackage?.Name ?? "Membership"),
                TrainerName = "HealthTrack Staff",
                Timestamp = sub.ActivatedAt,
                Status = sub.Status == SubscriptionStatus.Active ? "Active" : sub.Status.ToString(),
                StatusType = sub.Status == SubscriptionStatus.Active ? "green" : "purple"
            });
        }

        if (activities.Count < 5)
        {
            var recentUsersList = await database.Users.AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var u in recentUsersList)
            {
                if (!activities.Any(a => a.MemberEmail == u.Email))
                {
                    activities.Add(new MemberActivityItem
                    {
                        MemberName = u.FullName,
                        MemberEmail = u.Email ?? string.Empty,
                        Activity = "Account Registered",
                        MembershipType = "Standard",
                        TrainerName = "Unassigned",
                        Timestamp = u.CreatedAt,
                        Status = u.IsActive ? "Active" : "Inactive",
                        StatusType = u.IsActive ? "green" : "purple"
                    });
                }
            }
        }

        var orderedActivities = activities.OrderByDescending(a => a.Timestamp).Take(6).ToList();

        return new AdminDashboardViewModel
        {
            FullName = await database.Users.AsNoTracking().Where(x => x.Id == adminId)
                .Select(x => x.FullName).FirstOrDefaultAsync() ?? string.Empty,
            TotalUsers = await users.Users.CountAsync(),
            TotalClients = (await users.GetUsersInRoleAsync(AppRoles.Client)).Count,
            TotalTrainers = trainers.Count,
            ActiveUsers = await users.Users.CountAsync(x => x.IsActive),
            TotalAppointments = await database.Appointments.CountAsync(),
            ActiveWorkoutPlans = await database.WorkoutPlans.CountAsync(x => x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today),
            ActiveDietPlans = await database.DietPlans.CountAsync(x => x.Status == PlanStatus.Active && x.StartDate <= today && x.EndDate >= today),
            TotalRevenue = totalRevenue,
            NewSignups = newSignups,
            TotalPackages = totalPackages,
            ActiveSubscriptions = activeSubs,
            ChartLabels = chartLabels,
            ChartSignups = chartSignups,
            ChartBookings = chartBookings,
            RecentMemberActivities = orderedActivities,
            RecentUsers = await database.Users.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(5).Select(x => new DashboardUser(x.FullName, x.Email, x.CreatedAt, x.IsActive)).ToListAsync(),
            RecentAppointments = await database.Appointments.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Take(5).Select(x => new DashboardAppointment(x.Id, x.Client.FullName, x.Trainer != null ? x.Trainer.FullName : x.HealthcareProfessional!.User.FullName,
                    x.AppointmentDate, x.AppointmentTime, x.Status)).ToListAsync(),
            Trainers = trainers.OrderBy(x => x.FullName).ThenBy(x => x.Email)
                .Select(x => new TrainerListItem(x.Id, x.FullName, x.Email, x.IsActive, x.CreatedAt)).ToList(),
            AssignmentClients = await ActiveClients().OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync(),
            AssignmentTrainers = await ActiveTrainers().OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync(),
            Assignments = await database.TrainerClients.AsNoTracking().OrderByDescending(x => x.AssignedAt).ThenBy(x => x.ClientId)
                .Select(x => new AssignmentListItem(x.Client.FullName, x.Client.Email, x.Trainer.FullName, x.Trainer.Email, x.AssignedAt, x.ClientId)).ToListAsync()
        };
    }
}
