namespace MemorySystem.Application.Scopes;

public sealed record MemoryScopeResolveResult(
    bool Succeeded,
    MemoryScopeResolution? Resolution,
    string? Error)
{
    public static MemoryScopeResolveResult Success(MemoryScopeResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new MemoryScopeResolveResult(true, resolution, null);
    }

    public static MemoryScopeResolveResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new MemoryScopeResolveResult(false, null, error);
    }
}
