using HealthTrack.Models;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<TrainerClient> TrainerClients => Set<TrainerClient>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<DietMeal> DietMeals => Set<DietMeal>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<TrainerClient>().HasKey(x => new { x.TrainerId, x.ClientId });
        builder.Entity<TrainerClient>().HasIndex(x => x.ClientId).IsUnique();
        builder.Entity<TrainerClient>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TrainerClient>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkoutPlan>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkoutPlan>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DietPlan>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DietPlan>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkoutExercise>().HasOne(x => x.WorkoutPlan).WithMany(x => x.Exercises).HasForeignKey(x => x.WorkoutPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DietMeal>().HasOne(x => x.DietPlan).WithMany(x => x.Meals).HasForeignKey(x => x.DietPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkoutLog>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkoutLog>().HasOne(x => x.WorkoutPlan).WithMany().HasForeignKey(x => x.WorkoutPlanId).OnDelete(DeleteBehavior.Restrict);
        // Deleting an exercise preserves workout history; the optional exercise reference is cleared.
        builder.Entity<WorkoutLog>().HasOne(x => x.WorkoutExercise).WithMany().HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<MealLog>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MealLog>().HasOne(x => x.DietPlan).WithMany().HasForeignKey(x => x.DietPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DietMeal>().Property(x => x.Protein).HasPrecision(10, 2);
        builder.Entity<DietMeal>().Property(x => x.Carbohydrates).HasPrecision(10, 2);
        builder.Entity<DietMeal>().Property(x => x.Fat).HasPrecision(10, 2);
        builder.Entity<WorkoutLog>().HasIndex(x => new { x.ClientId, x.WorkoutDate });
        builder.Entity<MealLog>().HasIndex(x => new { x.ClientId, x.MealDate });
    }
}
