using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Training;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public static class FeaturePolicies
{
    public const string Trainer = "TrainerFeatures";
    public const string Client = "ClientFeatures";
}

public class PlanAccess(ApplicationDbContext database)
{
    public IQueryable<ApplicationUser> Clients(string trainerId) =>
        database.TrainerClients.Where(x => x.TrainerId == trainerId)
            .Select(x => x.Client).Where(x => x.IsActive && database.UserRoles.Any(ur =>
                ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Client))
                && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r =>
                    r.Id == ur.RoleId && (r.Name == AppRoles.Admin || r.Name == AppRoles.Trainer))));

    public IQueryable<WorkoutPlan> Workouts(string trainerId)
    {
        var clients = Clients(trainerId);
        return database.WorkoutPlans.Where(x => x.TrainerId == trainerId && clients.Any(c => c.Id == x.ClientId));
    }

    public IQueryable<DietPlan> Diets(string trainerId)
    {
        var clients = Clients(trainerId);
        return database.DietPlans.Where(x => x.TrainerId == trainerId && clients.Any(c => c.Id == x.ClientId));
    }
}
