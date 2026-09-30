using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Admin;
using HealthTrack.Models.Dashboard;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace HealthTrack.Services;

public class AdminDashboardData(ApplicationDbContext database, UserManager<ApplicationUser> users)
{
    private IQueryable<ApplicationUser> ActiveRole(string role) => database.Users.AsNoTracking().Where(x => x.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == role))
        && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin)));

    public IQueryable<ApplicationUser> ActiveClients() => ActiveRole(AppRoles.Client).Where(x =>
        !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Trainer)));
    public IQueryable<ApplicationUser> ActiveTrainers() => ActiveRole(AppRoles.Trainer);

    public async Task<AdminDashboardViewModel> LoadAsync(string? adminId = null)
    {
        var trainers = await users.GetUsersInRoleAsync(AppRoles.Trainer);
        return new AdminDashboardViewModel
        {
            FullName = await database.Users.AsNoTracking().Where(x => x.Id == adminId)
                .Select(x => x.FullName).FirstOrDefaultAsync() ?? string.Empty,
            TotalUsers = await users.Users.CountAsync(),
            TotalClients = (await users.GetUsersInRoleAsync(AppRoles.Client)).Count,
            TotalTrainers = trainers.Count,
            ActiveUsers = await users.Users.CountAsync(x => x.IsActive),
            TotalAppointments = await database.Appointments.CountAsync(),
            ActiveWorkoutPlans = await database.WorkoutPlans.CountAsync(x => x.Status == PlanStatus.Active),
            ActiveDietPlans = await database.DietPlans.CountAsync(x => x.Status == PlanStatus.Active),
            RecentUsers = await database.Users.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(5).Select(x => new DashboardUser(x.FullName, x.Email, x.CreatedAt, x.IsActive)).ToListAsync(),
            RecentAppointments = await database.Appointments.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Take(5).Select(x => new DashboardAppointment(x.Id, x.Client.FullName, x.Trainer.FullName,
                    x.AppointmentDate, x.AppointmentTime, x.Status)).ToListAsync(),
            Trainers = trainers.OrderBy(x => x.FullName).ThenBy(x => x.Email)
                .Select(x => new TrainerListItem(x.Id, x.FullName, x.Email, x.IsActive, x.CreatedAt)).ToList(),
            AssignmentClients = await ActiveClients().OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync(),
            AssignmentTrainers = await ActiveTrainers().OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync(),
            Assignments = await database.TrainerClients.AsNoTracking().OrderByDescending(x => x.AssignedAt).ThenBy(x => x.ClientId)
                .Select(x => new AssignmentListItem(x.Client.FullName, x.Client.Email, x.Trainer.FullName, x.Trainer.Email, x.AssignedAt)).ToListAsync()
        };
    }
}
