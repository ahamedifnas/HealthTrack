using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Models.Memberships;
using HealthTrack.Models.Payments;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<EmailVerificationOtp> EmailVerificationOtps => Set<EmailVerificationOtp>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();
    public DbSet<HealthcareProfessional> HealthcareProfessionals => Set<HealthcareProfessional>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();

    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();
    public DbSet<ClientSubscription> ClientSubscriptions => Set<ClientSubscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<TrainerClient> TrainerClients => Set<TrainerClient>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<DietMeal> DietMeals => Set<DietMeal>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TrainerAvailability> TrainerAvailabilities => Set<TrainerAvailability>();
    public DbSet<TrainerAvailabilityBreak> TrainerAvailabilityBreaks => Set<TrainerAvailabilityBreak>();
    public DbSet<TrainerUnavailability> TrainerUnavailabilities => Set<TrainerUnavailability>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<EmailVerificationOtp>().HasOne<ApplicationUser>().WithOne()
            .HasForeignKey<EmailVerificationOtp>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ReminderDelivery>().HasKey(x => new { x.UserId, x.ReminderKey });
        builder.Entity<ReminderDelivery>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<HealthcareProfessional>().HasOne(x => x.User).WithOne().HasForeignKey<HealthcareProfessional>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().HasOne(x => x.HealthcareProfessional).WithMany().HasForeignKey(x => x.HealthcareProfessionalId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().ToTable(t => t.HasCheckConstraint("CK_Appointment_ExactlyOneProvider",
            "([TrainerId] IS NOT NULL AND [HealthcareProfessionalId] IS NULL) OR ([TrainerId] IS NULL AND [HealthcareProfessionalId] IS NOT NULL)"));
        builder.Entity<Appointment>().HasIndex(x => new { x.HealthcareProfessionalId, x.AppointmentDate, x.AppointmentTime })
            .IsUnique().HasFilter("[Status] IN (1, 2) AND [HealthcareProfessionalId] IS NOT NULL");
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
            .IsUnique().HasFilter("[Status] IN (1, 2) AND [TrainerId] IS NOT NULL");
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
        // Archive exercises instead of destroying references from historical logs.
        builder.Entity<WorkoutLog>().HasOne(x => x.WorkoutExercise).WithMany().HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MealLog>().HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MealLog>().HasOne(x => x.DietPlan).WithMany().HasForeignKey(x => x.DietPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DietMeal>().Property(x => x.Protein).HasPrecision(10, 2);
        builder.Entity<DietMeal>().Property(x => x.Carbohydrates).HasPrecision(10, 2);
        builder.Entity<DietMeal>().Property(x => x.Fat).HasPrecision(10, 2);
        builder.Entity<WorkoutLog>().HasIndex(x => new { x.ClientId, x.WorkoutDate });
        builder.Entity<MealLog>().HasIndex(x => new { x.ClientId, x.MealDate });

        // Membership Packages & Subscriptions
        builder.Entity<MembershipPackage>().Property(x => x.PriceLKR).HasPrecision(18, 2);

        builder.Entity<PaymentTransaction>().Property(x => x.AmountLKR).HasPrecision(18, 2);
        builder.Entity<PaymentTransaction>().HasIndex(x => x.OrderId).IsUnique();
        builder.Entity<PaymentTransaction>().HasOne(x => x.Client).WithMany()
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PaymentTransaction>().HasOne(x => x.MembershipPackage).WithMany(x => x.Transactions)
            .HasForeignKey(x => x.MembershipPackageId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ClientSubscription>().Property(x => x.AmountPaidLKR).HasPrecision(18, 2);
        builder.Entity<ClientSubscription>().HasOne(x => x.Client).WithMany()
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ClientSubscription>().HasOne(x => x.MembershipPackage).WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.MembershipPackageId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ClientSubscription>().HasOne(x => x.PaymentTransaction).WithOne(x => x.Subscription)
            .HasForeignKey<ClientSubscription>(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ClientSubscription>().HasIndex(x => new { x.ClientId, x.Status, x.EndDate });

        // Trainer Availability & Leave
        builder.Entity<TrainerAvailability>().Property(x => x.StartTime).HasPrecision(0);
        builder.Entity<TrainerAvailability>().Property(x => x.EndTime).HasPrecision(0);
        builder.Entity<TrainerAvailability>().HasOne(x => x.Trainer).WithMany()
            .HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TrainerAvailability>().HasIndex(x => new { x.TrainerId, x.DayOfWeek, x.IsActive });

        builder.Entity<TrainerAvailabilityBreak>().Property(x => x.BreakStartTime).HasPrecision(0);
        builder.Entity<TrainerAvailabilityBreak>().Property(x => x.BreakEndTime).HasPrecision(0);
        builder.Entity<TrainerAvailabilityBreak>().HasOne(x => x.TrainerAvailability).WithMany(x => x.Breaks)
            .HasForeignKey(x => x.TrainerAvailabilityId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TrainerUnavailability>().Property(x => x.StartTime).HasPrecision(0);
        builder.Entity<TrainerUnavailability>().Property(x => x.EndTime).HasPrecision(0);
        builder.Entity<TrainerUnavailability>().HasOne(x => x.Trainer).WithMany()
            .HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TrainerUnavailability>().HasIndex(x => new { x.TrainerId, x.StartDate, x.EndDate, x.Status });
    }
}
