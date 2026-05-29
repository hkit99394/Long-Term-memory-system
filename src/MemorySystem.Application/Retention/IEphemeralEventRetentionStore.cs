namespace MemorySystem.Application.Retention;

public interface IEphemeralEventRetentionStore
{
    Task<EphemeralEventMinimizationResult> MinimizeExpiredAsync(
        EphemeralEventMinimizationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record EphemeralEventMinimizationCommand(
    DateTimeOffset Cutoff,
    int BatchSize);

public sealed record EphemeralEventMinimizationResult(int MinimizedEvents);
