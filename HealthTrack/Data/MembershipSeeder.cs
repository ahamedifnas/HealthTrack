using HealthTrack.Models.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data;

public static class MembershipSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var database = services.GetRequiredService<ApplicationDbContext>();
        if (await database.MembershipPackages.AnyAsync()) return;

        var starter = new MembershipPackage
        {
            Name = "Basic Starter",
            Description = "Essential health and fitness tracking for beginners. Includes standard access and measurement logs.",
            PriceLKR = 3500.00m,
            DurationDays = 30,
            BenefitsJson = "[\"Basic health measurements & BMI tracking\",\"Access to view assigned workout & diet plans\",\"Personal workout & meal activity logging\",\"In-app notification reminders\"]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var pro = new MembershipPackage
        {
            Name = "Pro Fitness",
            Description = "Our most popular plan. Direct trainer guidance, tailored workout & diet regimens, and consultation booking.",
            PriceLKR = 8500.00m,
            DurationDays = 90,
            BenefitsJson = "[\"All Basic Starter features\",\"Dedicated Trainer assignment\",\"Custom Workout & Diet plan management\",\"Appointment booking with certified trainers\",\"Progress analytics and health reports\",\"Downloadable PDF progress summaries\"]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var elite = new MembershipPackage
        {
            Name = "Elite Health",
            Description = "Comprehensive annual premium health management with full access to trainers and healthcare professionals.",
            PriceLKR = 28000.00m,
            DurationDays = 365,
            BenefitsJson = "[\"All Pro Fitness features\",\"Priority appointment scheduling\",\"Consultations with Healthcare Professionals\",\"Advanced clinical health metrics & analytics\",\"Unlimited PDF report exports\",\"Dedicated priority customer support\"]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        database.MembershipPackages.AddRange(starter, pro, elite);
        await database.SaveChangesAsync();
    }
}
