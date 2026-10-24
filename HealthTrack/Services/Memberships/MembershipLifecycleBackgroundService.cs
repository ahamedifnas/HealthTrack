namespace HealthTrack.Services.Memberships;

public class MembershipLifecycleBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MembershipLifecycleBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MembershipLifecycleBackgroundService started.");

        // Brief delay upon initial boot to allow EF migrations/startup to complete cleanly
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMembershipSubscriptionService>();
                var processed = await service.ProcessExpirationsAndAlertsAsync();
                if (processed > 0)
                {
                    logger.LogInformation("MembershipLifecycleBackgroundService processed {Count} subscription expiration/alert event(s).", processed);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error occurred during periodic membership lifecycle processing.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("MembershipLifecycleBackgroundService stopped.");
    }
}
