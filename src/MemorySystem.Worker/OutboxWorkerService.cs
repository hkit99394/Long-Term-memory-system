using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public sealed class OutboxWorkerService : BackgroundService
{
    private readonly OutboxJobProcessor processor;
    private readonly OutboxWorkerOptions options;
    private readonly ILogger<OutboxWorkerService> logger;

    public OutboxWorkerService(
        OutboxJobProcessor processor,
        IOptions<OutboxWorkerOptions> options,
        ILogger<OutboxWorkerService> logger)
    {
        if (!processor.HasHandlers)
        {
            throw new InvalidOperationException(
                "Outbox worker is enabled, but no outbox job handlers are registered.");
        }

        this.processor = processor;
        this.options = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox worker {WorkerId} started.", options.WorkerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await processor.ProcessAvailableAsync(stoppingToken);
                var delay = processedCount == 0 ? options.IdleDelay : TimeSpan.Zero;

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox worker {WorkerId} failed while processing jobs.", options.WorkerId);
                await Task.Delay(options.ErrorDelay, stoppingToken);
            }
        }

        logger.LogInformation("Outbox worker {WorkerId} stopped.", options.WorkerId);
    }
}
