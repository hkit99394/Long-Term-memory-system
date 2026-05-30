using System.Collections.Concurrent;

namespace MemorySystem.Api.Operations;

public sealed class ApiRequestMetricsStore
{
    private readonly ConcurrentDictionary<ApiRequestMetricKey, ApiRequestMetricCounter> counters = new();

    public void Record(string method, string route, int statusCode, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var key = new ApiRequestMetricKey(
            method.ToUpperInvariant(),
            route,
            statusCode);
        var counter = counters.GetOrAdd(key, _ => new ApiRequestMetricCounter());
        counter.Record(duration, statusCode >= StatusCodes.Status500InternalServerError);
    }

    public IReadOnlyList<ApiRequestMetricSnapshot> Read()
    {
        return counters
            .Select(entry => entry.Value.Read(entry.Key))
            .OrderBy(snapshot => snapshot.Route, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Method, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.StatusCode)
            .ToArray();
    }
}

public sealed record ApiRequestMetricSnapshot(
    string Method,
    string Route,
    int StatusCode,
    long Count,
    long FailureCount,
    double DurationSecondsSum);

internal readonly record struct ApiRequestMetricKey(
    string Method,
    string Route,
    int StatusCode);

internal sealed class ApiRequestMetricCounter
{
    private long count;
    private long failureCount;
    private long durationTicks;

    public void Record(TimeSpan duration, bool failed)
    {
        Interlocked.Increment(ref count);
        Interlocked.Add(ref durationTicks, Math.Max(0, duration.Ticks));

        if (failed)
        {
            Interlocked.Increment(ref failureCount);
        }
    }

    public ApiRequestMetricSnapshot Read(ApiRequestMetricKey key)
    {
        return new ApiRequestMetricSnapshot(
            key.Method,
            key.Route,
            key.StatusCode,
            Interlocked.Read(ref count),
            Interlocked.Read(ref failureCount),
            Interlocked.Read(ref durationTicks) / (double)TimeSpan.TicksPerSecond);
    }
}
