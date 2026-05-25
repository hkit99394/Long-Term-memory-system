using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Idempotency;

public sealed class Sha256ApiRequestHasher(IOptions<ApiIdempotencyOptions> options) : IApiRequestHasher
{
    private const string Prefix = "sha256:";
    private const int BufferThreshold = 30 * 1024;

    public async Task<string> ComputeHashAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxBodyBytes = options.Value.MaxBodyBytes;

        request.EnableBuffering(BufferThreshold, maxBodyBytes);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var totalBytesRead = 0L;

        try
        {
            while (true)
            {
                var bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;

                if (totalBytesRead > maxBodyBytes)
                {
                    throw new ApiRequestBodyTooLargeException(maxBodyBytes);
                }

                hash.AppendData(buffer.AsSpan(0, bytesRead));
            }
        }
        catch (IOException exception)
        {
            throw new ApiRequestBodyTooLargeException(maxBodyBytes, exception);
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
