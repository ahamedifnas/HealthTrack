using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Trainer, AppRoles.Client })
        {
            if (!await roles.RoleExistsAsync(role))
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole(role)));
        }

        if (!environment.IsDevelopment()) return;

        var email = configuration["DevelopmentAdmin:Email"];
        var password = configuration["DevelopmentAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set both DevelopmentAdmin:Email and DevelopmentAdmin:Password.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Never elevate a pre-existing public account based only on its email address.
            if (!await users.IsInRoleAsync(existing, AppRoles.Admin))
                throw new InvalidOperationException("The development admin email belongs to a non-admin account. Use a different email.");
            return;
        }

        var database = services.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var admin = new ApplicationUser
        {
            UserName = email.Trim(), Email = email.Trim(),
            FullName = "Development Admin", EmailConfirmed = true
        };
        EnsureSucceeded(await users.CreateAsync(admin, password));
        EnsureSucceeded(await users.AddToRoleAsync(admin, AppRoles.Admin));
        await transaction.CommitAsync();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
