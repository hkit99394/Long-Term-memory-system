namespace MemorySystem.Api.Idempotency;

public interface IApiRequestHasher
{
    Task<string> ComputeHashAsync(HttpRequest request, CancellationToken cancellationToken = default);
}
