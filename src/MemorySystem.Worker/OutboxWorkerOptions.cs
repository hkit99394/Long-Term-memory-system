using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace MemorySystem.Worker;

public sealed class OutboxWorkerOptions
{
    public bool Enabled { get; init; } = true;

    public string WorkerId { get; init; } = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public int BatchSize { get; init; } = 10;

    public int MaxAttempts { get; init; } = 3;

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan HandlerTimeout { get; init; } = TimeSpan.FromMinutes(4);

    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    public static OutboxWorkerOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("OutboxWorker");
        var defaults = new OutboxWorkerOptions();
        var options = new OutboxWorkerOptions
        {
            WorkerId = GetString(section, nameof(WorkerId), defaults.WorkerId),
            Enabled = GetBool(section, nameof(Enabled), defaults.Enabled),
            BatchSize = GetInt(section, nameof(BatchSize), defaults.BatchSize),
            MaxAttempts = GetInt(section, nameof(MaxAttempts), defaults.MaxAttempts),
            LeaseDuration = GetTimeSpan(section, nameof(LeaseDuration), defaults.LeaseDuration),
            HandlerTimeout = GetTimeSpan(section, nameof(HandlerTimeout), defaults.HandlerTimeout),
            IdleDelay = GetTimeSpan(section, nameof(IdleDelay), defaults.IdleDelay),
            ErrorDelay = GetTimeSpan(section, nameof(ErrorDelay), defaults.ErrorDelay),
            RetryDelay = GetTimeSpan(section, nameof(RetryDelay), defaults.RetryDelay)
        };

        options.Validate();

        return options;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkerId))
        {
            throw new InvalidOperationException("Outbox worker id must be configured.");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("Outbox worker batch size must be greater than zero.");
        }

        if (MaxAttempts <= 0)
        {
            throw new InvalidOperationException("Outbox worker max attempts must be greater than zero.");
        }

        if (LeaseDuration <= TimeSpan.Zero || HandlerTimeout <= TimeSpan.Zero || IdleDelay <= TimeSpan.Zero || ErrorDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Outbox worker durations must be greater than zero.");
        }

        if (HandlerTimeout >= LeaseDuration)
        {
            throw new InvalidOperationException("Outbox worker handler timeout must be shorter than the lease duration.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Outbox worker retry delay cannot be negative.");
        }
    }

    private static string GetString(IConfiguration section, string key, string fallback)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
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
