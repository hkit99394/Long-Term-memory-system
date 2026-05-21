using System.Buffers;
using System.Security.Cryptography;

namespace MemorySystem.Api.Idempotency;

public sealed class Sha256ApiRequestHasher : IApiRequestHasher
{
    private const string Prefix = "sha256:";

    public async Task<string> ComputeHashAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.EnableBuffering();

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            while (true)
            {
                var bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                hash.AppendData(buffer.AsSpan(0, bytesRead));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }

        return Prefix + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
