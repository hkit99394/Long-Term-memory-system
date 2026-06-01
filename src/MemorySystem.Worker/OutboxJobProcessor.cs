using System.Diagnostics;
using System.Diagnostics.Metrics;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.Observability;
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
        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.WorkerOutboxLeaseSpanName,
            ActivityKind.Consumer);
        activity?.SetTag("memorysystem.worker_type", OutboxWorkerService.WorkerType);
        activity?.SetTag("memorysystem.worker_id", options.WorkerId);
        activity?.SetTag("memorysystem.batch_size", options.BatchSize);
        activity?.SetTag("memorysystem.job_count", 0);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        IReadOnlyList<OutboxJob> jobs;

        try
        {
            jobs = await jobStore.LeaseAvailableAsync(
                options.WorkerId,
                options.BatchSize,
                options.LeaseDuration,
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "lease_failed");
            throw;
        }

        activity?.SetTag("memorysystem.job_count", jobs.Count);
        var leaseTags = new TagList
        {
            { "memorysystem.worker_type", OutboxWorkerService.WorkerType },
            { "memorysystem.worker_id", options.WorkerId },
            { "memorysystem.batch_size", options.BatchSize },
            { "memorysystem.job_count", jobs.Count }
        };
        MemorySystemTelemetry.OutboxLeaseCounter.Add(1, leaseTags);

        foreach (var job in jobs)
        {
            await ProcessAsync(job, cancellationToken);
        }

        return jobs.Count;
    }

    private async Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
    {
        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.WorkerOutboxJobSpanName,
            ActivityKind.Consumer);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["WorkerType"] = OutboxWorkerService.WorkerType,
            ["WorkerId"] = options.WorkerId,
            ["OutboxJobId"] = job.Id,
            ["OutboxJobType"] = job.JobType
        });

        var stopwatch = Stopwatch.StartNew();
        var outcome = "completed";
        var failureStatus = "none";

        activity?.SetTag("memorysystem.worker_type", OutboxWorkerService.WorkerType);
        activity?.SetTag("memorysystem.worker_id", options.WorkerId);
        activity?.SetTag("memorysystem.job_type", job.JobType);
        activity?.SetTag("memorysystem.aggregate_type", job.AggregateType);
        activity?.SetTag("memorysystem.attempts", job.Attempts);

        try
        {
            if (job.Attempts > options.MaxAttempts)
            {
                outcome = "dead_letter";
                failureStatus = "max_attempts";
                activity?.SetStatus(ActivityStatusCode.Error);
                await MarkFailedAsync(
                    job,
                    $"Outbox job exceeded the configured max attempts ({options.MaxAttempts}).",
                    cancellationToken);

                return;
            }

            var handler = handlers.FirstOrDefault(candidate => candidate.CanHandle(job.JobType));

            if (handler is null)
            {
                outcome = "unsupported";
                failureStatus = "unsupported_job_type";
                activity?.SetStatus(ActivityStatusCode.Error);
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
                    outcome = "lease_lost";
                    failureStatus = "lease_mismatch";
                    logger.LogWarning("Outbox job {JobId} was not completed because its lease no longer matched.", job.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outcome = "cancelled";
                failureStatus = "host_shutdown";
                throw;
            }
            catch (Exception exception)
            {
                outcome = job.Attempts >= options.MaxAttempts ? "dead_letter" : "retry";
                failureStatus = "handler_exception";
                activity?.SetStatus(ActivityStatusCode.Error);
                await MarkFailedAsync(job, exception.Message, cancellationToken);
            }
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("memorysystem.outcome", outcome);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, failureStatus);

            var tags = new TagList
            {
                { "memorysystem.worker_type", OutboxWorkerService.WorkerType },
                { "memorysystem.worker_id", options.WorkerId },
                { "memorysystem.job_type", job.JobType },
                { "memorysystem.aggregate_type", job.AggregateType },
                { "memorysystem.outcome", outcome }
            };

            MemorySystemTelemetry.OutboxJobCounter.Add(1, tags);
            MemorySystemTelemetry.OutboxJobDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
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
