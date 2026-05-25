using MemorySystem.Infrastructure.Workers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public sealed class OutboxWorkerService : BackgroundService
{
    public const string WorkerType = WorkerHeartbeatTypes.Outbox;

    private readonly OutboxJobProcessor processor;
    private readonly OutboxWorkerOptions options;
    private readonly IWorkerHeartbeatStore heartbeatStore;
    private readonly ILogger<OutboxWorkerService> logger;

    public OutboxWorkerService(
        OutboxJobProcessor processor,
        IOptions<OutboxWorkerOptions> options,
        IWorkerHeartbeatStore heartbeatStore,
        ILogger<OutboxWorkerService> logger)
    {
        this.processor = processor;
        this.options = options.Value;
        this.heartbeatStore = heartbeatStore;
        this.logger = logger;

        OutboxWorkerReadiness.ThrowIfCannotStart(this.options, processor.HandlerCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox worker {WorkerId} started.", options.WorkerId);
        await RecordHeartbeatAsync(WorkerHeartbeatStatuses.Starting, lastError: null, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await processor.ProcessAvailableAsync(stoppingToken);
                await RecordHeartbeatAsync(WorkerHeartbeatStatuses.Running, lastError: null, stoppingToken);

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
                await RecordHeartbeatAsync(WorkerHeartbeatStatuses.Error, exception.Message, stoppingToken);
                await Task.Delay(options.ErrorDelay, stoppingToken);
            }
        }

        await RecordHeartbeatAsync(WorkerHeartbeatStatuses.Stopped, lastError: null, CancellationToken.None);
        logger.LogInformation("Outbox worker {WorkerId} stopped.", options.WorkerId);
    }

    private async Task RecordHeartbeatAsync(
        string status,
        string? lastError,
        CancellationToken cancellationToken)
    {
        try
        {
            await heartbeatStore.RecordAsync(
                new WorkerHeartbeatUpdate(
                    WorkerType,
                    options.WorkerId,
                    status,
                    lastError),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Outbox worker {WorkerId} could not record a {Status} heartbeat.",
                options.WorkerId,
                status);
        }
    }
}
