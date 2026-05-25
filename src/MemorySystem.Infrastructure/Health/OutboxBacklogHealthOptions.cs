using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace MemorySystem.Infrastructure.Health;

public sealed class OutboxBacklogHealthOptions
{
    public long MaxReadyPendingJobs { get; init; } = 100;

    public TimeSpan MaxReadyPendingAge { get; init; } = TimeSpan.FromMinutes(5);

    public static OutboxBacklogHealthOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("OutboxBacklogHealth");
        var defaults = new OutboxBacklogHealthOptions();
        var options = new OutboxBacklogHealthOptions
        {
            MaxReadyPendingJobs = GetLong(section, nameof(MaxReadyPendingJobs), defaults.MaxReadyPendingJobs),
            MaxReadyPendingAge = GetTimeSpan(section, nameof(MaxReadyPendingAge), defaults.MaxReadyPendingAge)
        };

        if (options.MaxReadyPendingJobs < 0)
        {
            throw new InvalidOperationException("Outbox backlog health max ready pending jobs cannot be negative.");
        }

        if (options.MaxReadyPendingAge < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Outbox backlog health max ready pending age cannot be negative.");
        }

        return options;
    }

    private static long GetLong(IConfiguration section, string key, long fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : long.Parse(value, CultureInfo.InvariantCulture);
    }

    private static TimeSpan GetTimeSpan(IConfiguration section, string key, TimeSpan fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }
}
