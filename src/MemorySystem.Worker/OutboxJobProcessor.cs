using MemorySystem.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public sealed class OutboxJobProcessor(
    IOutboxJobStore jobStore,
    IEnumerable<IOutboxJobHandler> handlers,
    IOptions<OutboxWorkerOptions> options,
    ILogger<OutboxJobProcessor> logger)
{
    private readonly IOutboxJobHandler[] handlers = handlers.ToArray();
    private readonly OutboxWorkerOptions options = options.Value;

    public int HandlerCount => handlers.Length;

    public bool HasHandlers => handlers.Length > 0;

    public async Task<int> ProcessAvailableAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await jobStore.LeaseAvailableAsync(
            options.WorkerId,
            options.BatchSize,
            options.LeaseDuration,
            cancellationToken);

        foreach (var job in jobs)
        {
            await ProcessAsync(job, cancellationToken);
        }

        return jobs.Count;
    }

    private async Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
    {
        if (job.Attempts > options.MaxAttempts)
        {
            await MarkFailedAsync(
                job,
                $"Outbox job exceeded the configured max attempts ({options.MaxAttempts}).",
                cancellationToken);

            return;
        }

        var handler = handlers.FirstOrDefault(candidate => candidate.CanHandle(job.JobType));

        if (handler is null)
        {
            await MarkFailedAsync(
                job,
                $"Unsupported outbox job type '{job.JobType}'.",
                cancellationToken);

            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.HandlerTimeout);

            await handler.ProcessAsync(job, timeout.Token);

            if (!await jobStore.CompleteAsync(job, cancellationToken))
            {
                logger.LogWarning("Outbox job {JobId} was not completed because its lease no longer matched.", job.Id);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(job, exception.Message, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(OutboxJob job, string error, CancellationToken cancellationToken)
    {
        var deadLetter = job.Attempts >= options.MaxAttempts;
        DateTimeOffset? availableAt = deadLetter ? null : DateTimeOffset.UtcNow.Add(options.RetryDelay);

        if (!await jobStore.MarkFailedAsync(job, error, deadLetter, availableAt, cancellationToken))
        {
            logger.LogWarning("Outbox job {JobId} failure was not recorded because its lease no longer matched.", job.Id);
        }
    }
}
