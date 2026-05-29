using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace MemorySystem.Worker;

public sealed class EphemeralEventRetentionOptions
{
    public bool Enabled { get; init; } = true;

    public TimeSpan MaxAge { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    public int BatchSize { get; init; } = 100;

    public bool RunOnStartup { get; init; } = true;

    public static EphemeralEventRetentionOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("EphemeralEventRetention");
        var defaults = new EphemeralEventRetentionOptions();
        var options = new EphemeralEventRetentionOptions
        {
            Enabled = GetBool(section, nameof(Enabled), defaults.Enabled),
            MaxAge = GetTimeSpan(section, nameof(MaxAge), defaults.MaxAge),
            Interval = GetTimeSpan(section, nameof(Interval), defaults.Interval),
            BatchSize = GetInt(section, nameof(BatchSize), defaults.BatchSize),
            RunOnStartup = GetBool(section, nameof(RunOnStartup), defaults.RunOnStartup)
        };

        options.Validate();

        return options;
    }

    private void Validate()
    {
        if (MaxAge <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Ephemeral event retention max age must be greater than zero.");
        }

        if (Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Ephemeral event retention interval must be greater than zero.");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("Ephemeral event retention batch size must be greater than zero.");
        }
    }

    private static int GetInt(IConfiguration section, string key, int fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static bool GetBool(IConfiguration section, string key, bool fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : bool.Parse(value);
    }

    private static TimeSpan GetTimeSpan(IConfiguration section, string key, TimeSpan fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }
}
