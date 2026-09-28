using System.Data;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

// Local setup command only; never exposed as an HTTP endpoint.
public static class DevelopmentAssignment
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Assignment setup is available only in Development.");

        var trainerEmail = configuration["Assignment:TrainerEmail"]?.Trim();
        var clientEmail = configuration["Assignment:ClientEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(trainerEmail) || string.IsNullOrWhiteSpace(clientEmail))
            throw new InvalidOperationException("TrainerEmail and ClientEmail are required.");

        var database = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var trainer = await users.FindByEmailAsync(trainerEmail);
        var client = await users.FindByEmailAsync(clientEmail);
        if (trainer is null || client is null || !trainer.IsActive || !client.IsActive)
            throw new InvalidOperationException("Register both active accounts and start the app to seed roles first.");
        if (trainer.Id == client.Id)
            throw new InvalidOperationException("Trainer and Client must be different accounts.");
        if (await users.IsInRoleAsync(trainer, AppRoles.Admin) || await users.IsInRoleAsync(client, AppRoles.Admin))
            throw new InvalidOperationException("Admin accounts cannot be used for this development assignment.");
        if (!await users.IsInRoleAsync(client, AppRoles.Client) || await users.IsInRoleAsync(client, AppRoles.Trainer))
            throw new InvalidOperationException("The client must have only the Client role.");

        if (configuration.GetValue<bool>("Assignment:PromoteTrainer"))
        {
            if (await database.TrainerClients.AnyAsync(x => x.ClientId == trainer.Id))
                throw new InvalidOperationException("An assigned client cannot be promoted by this setup command.");
            if (await users.IsInRoleAsync(trainer, AppRoles.Client))
                EnsureSucceeded(await users.RemoveFromRoleAsync(trainer, AppRoles.Client));
            if (!await users.IsInRoleAsync(trainer, AppRoles.Trainer))
                EnsureSucceeded(await users.AddToRoleAsync(trainer, AppRoles.Trainer));
            EnsureSucceeded(await users.UpdateSecurityStampAsync(trainer));
        }
        if (!await users.IsInRoleAsync(trainer, AppRoles.Trainer))
            throw new InvalidOperationException("The trainer needs the Trainer role. For a test account, use PromoteTrainer.");

        var existing = await database.TrainerClients.SingleOrDefaultAsync(x => x.ClientId == client.Id);
        if (existing is not null && existing.TrainerId != trainer.Id)
            throw new InvalidOperationException("This client is already assigned to another trainer. No reassignment was made.");
        if (existing is null)
        {
            database.TrainerClients.Add(new TrainerClient { TrainerId = trainer.Id, ClientId = client.Id });
            await database.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        Console.WriteLine("Development assignment saved. Log in again to refresh role claims.");
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
