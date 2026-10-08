using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Trainer, AppRoles.Client, AppRoles.HealthcareProfessional })
        {
            if (!await roles.RoleExistsAsync(role))
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole(role)));
        }

        if (!environment.IsDevelopment()) return;

        // Read only user-secrets: appsettings, command-line and environment values cannot seed a password.
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets(typeof(IdentitySeeder).Assembly, optional: true);
        var email = configuration["Admin:Email"]?.Trim();
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set both Admin:Email and Admin:Password for development Admin seeding.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is not null && (await users.IsInRoleAsync(admin, AppRoles.Client)
            || await users.IsInRoleAsync(admin, AppRoles.Trainer) || await users.IsInRoleAsync(admin, AppRoles.HealthcareProfessional)))
        {
            throw new InvalidOperationException("The configured Admin email belongs to a Client or Trainer. That account will not be changed.");
        }

        var database = services.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync();
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email, Email = email,
                FullName = "HealthTrack Admin", IsActive = true, EmailConfirmed = true
            };
            EnsureSucceeded(await users.CreateAsync(admin, password));
        }
        else
        {
            admin.IsActive = true;
            EnsureSucceeded(await users.UpdateAsync(admin));
            // Identity validates and hashes the configured password; never write PasswordHash directly.
            var token = await users.GeneratePasswordResetTokenAsync(admin);
            EnsureSucceeded(await users.ResetPasswordAsync(admin, token, password));
            EnsureSucceeded(await users.SetLockoutEndDateAsync(admin, null));
            EnsureSucceeded(await users.ResetAccessFailedCountAsync(admin));
        }

        if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
            EnsureSucceeded(await users.AddToRoleAsync(admin, AppRoles.Admin));
        await transaction.CommitAsync();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
