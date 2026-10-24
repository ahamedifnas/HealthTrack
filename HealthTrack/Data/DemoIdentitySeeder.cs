using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Data;

public static class DemoIdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Client, AppRoles.Trainer, AppRoles.HealthcareProfessional })
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var entry in new[] { ("client", AppRoles.Client), ("client2", AppRoles.Client), ("admin", AppRoles.Admin) })
        {
            var email = $"{entry.Item1}@healthtrack.test";
            if (await users.FindByEmailAsync(email) is not null) continue;
            var user = new ApplicationUser { UserName = email, Email = email, FullName = $"Demo {entry.Item1}", EmailConfirmed = true, IsActive = true };
            Check(await users.CreateAsync(user, "LocalDemo!2026"));
            Check(await users.AddToRoleAsync(user, entry.Item2));
        }
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
