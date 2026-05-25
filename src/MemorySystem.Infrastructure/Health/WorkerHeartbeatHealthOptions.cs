using System.Globalization;
using MemorySystem.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.Infrastructure.Health;

public sealed class WorkerHeartbeatHealthOptions
{
    public string WorkerType { get; init; } = WorkerHeartbeatTypes.Outbox;

    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMinutes(2);

    public static WorkerHeartbeatHealthOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("WorkerHeartbeatHealth");
        var defaults = new WorkerHeartbeatHealthOptions();
        var options = new WorkerHeartbeatHealthOptions
        {
            WorkerType = GetString(section, nameof(WorkerType), defaults.WorkerType),
            MaxAge = GetTimeSpan(section, nameof(MaxAge), defaults.MaxAge)
        };

        if (string.IsNullOrWhiteSpace(options.WorkerType))
        {
            throw new InvalidOperationException("Worker heartbeat health worker type must be configured.");
        }

        if (options.MaxAge <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Worker heartbeat health max age must be greater than zero.");
        }

        return options;
    }

    private static string GetString(IConfiguration section, string key, string fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static TimeSpan GetTimeSpan(IConfiguration section, string key, TimeSpan fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }
}
