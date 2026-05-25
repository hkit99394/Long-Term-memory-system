namespace MemorySystem.Api.Idempotency;

public interface IApiRequestHasher
{
    Task<ApiRequestHashes> ComputeHashAsync(HttpRequest request, CancellationToken cancellationToken = default);
}

public sealed record ApiRequestHashes(
    string CurrentHash,
    IReadOnlyList<string> AcceptedHashes);
