using MemorySystem.Application.Retention;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public sealed class EphemeralEventRetentionService(
    IEphemeralEventRetentionStore retentionStore,
    IOptions<EphemeralEventRetentionOptions> options,
    ILogger<EphemeralEventRetentionService> logger) : BackgroundService
{
    private readonly EphemeralEventRetentionOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Ephemeral event retention worker is disabled.");
            return;
        }

        logger.LogInformation(
            "Ephemeral event retention worker started. MaxAge={MaxAge} Interval={Interval} BatchSize={BatchSize}",
            options.MaxAge,
            options.Interval,
            options.BatchSize);

        if (options.RunOnStartup)
        {
            await MinimizeExpiredEventsAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(options.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await MinimizeExpiredEventsAsync(stoppingToken);
        }
    }

    private async Task MinimizeExpiredEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(options.MaxAge);
            var result = await retentionStore.MinimizeExpiredAsync(
                new EphemeralEventMinimizationCommand(cutoff, options.BatchSize),
                cancellationToken);

            if (result.MinimizedEvents > 0)
            {
                logger.LogInformation(
                    "Minimized {MinimizedEvents} expired ephemeral source events.",
                    result.MinimizedEvents);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ephemeral event retention worker failed while minimizing expired events.");
        }
    }
}
