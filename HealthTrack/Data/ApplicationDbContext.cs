using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();

    public DbSet<TrainerClient> TrainerClients => Set<TrainerClient>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<DietMeal> DietMeals => Set<DietMeal>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Notification>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        builder.Entity<ApplicationUser>().Property(x => x.TargetWeight).HasPrecision(5, 2);
        builder.Entity<HealthMeasurement>().HasOne(x => x.Client).WithMany(x => x.HealthMeasurements)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HealthMeasurement>().HasIndex(x => new { x.ClientId, x.MeasurementDate }).IsUnique();
        builder.Entity<HealthMeasurement>().Property(x => x.WeightKg).HasPrecision(5, 2);
        builder.Entity<HealthMeasurement>().Property(x => x.HeightCm).HasPrecision(5, 2);
        builder.Entity<HealthMeasurement>().Property(x => x.BMI).HasPrecision(7, 2);
        builder.Entity<HealthMeasurement>().Property(x => x.BodyFatPercentage).HasPrecision(5, 2);
        builder.Entity<Appointment>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().Property(x => x.AppointmentTime).HasPrecision(0);
        // Only Pending/Approved records reserve a slot. The database also prevents racing bookings.
        builder.Entity<Appointment>().HasIndex(x => new { x.TrainerId, x.AppointmentDate, x.AppointmentTime })
            .IsUnique().HasFilter("[Status] IN (1, 2)");
        builder.Entity<Appointment>().HasIndex(x => new { x.ClientId, x.AppointmentDate, x.AppointmentTime })
            .IsUnique().HasFilter("[Status] IN (1, 2)");
        builder.Entity<TrainerClient>().Property(x => x.AssignedAt).HasDefaultValueSql("GETUTCDATE()");
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
