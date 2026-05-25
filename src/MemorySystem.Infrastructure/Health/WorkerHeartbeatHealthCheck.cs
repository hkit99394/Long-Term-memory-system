using MemorySystem.Infrastructure.Workers;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MemorySystem.Infrastructure.Health;

public sealed class WorkerHeartbeatHealthCheck(
    IWorkerHeartbeatStore heartbeatStore,
    WorkerHeartbeatHealthOptions options) : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var heartbeat = await heartbeatStore.ReadLatestAsync(options.WorkerType, cancellationToken);

            if (heartbeat is null)
            {
                return HealthCheckResult.Degraded(
                    $"No {options.WorkerType} worker heartbeat has been observed.",
                    data: BuildData(null, null));
            }

            var age = DateTimeOffset.UtcNow - heartbeat.LastSeenAt;
            var data = BuildData(heartbeat, age);

            if (age > options.MaxAge)
            {
                return HealthCheckResult.Degraded(
                    $"{options.WorkerType} worker heartbeat is stale.",
                    data: data);
            }

            if (string.Equals(heartbeat.Status, WorkerHeartbeatStatuses.Error, StringComparison.Ordinal))
            {
                return HealthCheckResult.Degraded(
                    $"{options.WorkerType} worker heartbeat reports processing errors.",
                    data: data);
            }

            if (string.Equals(heartbeat.Status, WorkerHeartbeatStatuses.Stopped, StringComparison.Ordinal))
            {
                return HealthCheckResult.Degraded(
                    $"{options.WorkerType} worker heartbeat reports a stopped worker.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"{options.WorkerType} worker heartbeat is fresh.",
                data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                $"{options.WorkerType} worker heartbeat is unreachable.",
                exception);
        }
    }

    private Dictionary<string, object> BuildData(
        WorkerHeartbeatSnapshot? heartbeat,
        TimeSpan? age)
    {
        var data = new Dictionary<string, object>
        {
            ["workerType"] = options.WorkerType,
            ["maxAgeSeconds"] = options.MaxAge.TotalSeconds
        };

        if (heartbeat is null)
        {
            return data;
        }

        data["workerId"] = heartbeat.WorkerId;
        data["status"] = heartbeat.Status;
        data["lastSeenAt"] = heartbeat.LastSeenAt;
        data["lastSeenAgeSeconds"] = age?.TotalSeconds ?? 0;

        if (heartbeat.LastSuccessAt.HasValue)
        {
            data["lastSuccessAt"] = heartbeat.LastSuccessAt.Value;
        }

        if (!string.IsNullOrWhiteSpace(heartbeat.LastError))
        {
            data["lastError"] = heartbeat.LastError;
        }

        return data;
    }
}
