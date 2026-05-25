using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MemorySystem.Api.Idempotency;

public sealed class Sha256ApiRequestHasher(IOptions<ApiIdempotencyOptions> options) : IApiRequestHasher
{
    private const string CurrentPrefix = "sha256:v2:";
    private const string LegacyBodyOnlyPrefix = "sha256:";
    private const int BufferThreshold = 30 * 1024;

    public async Task<ApiRequestHashes> ComputeHashAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxBodyBytes = options.Value.MaxBodyBytes;

        request.EnableBuffering(BufferThreshold, maxBodyBytes);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        using var currentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var legacyBodyOnlyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashField(currentHash, "method", request.Method);
        AppendHashField(currentHash, "path", request.Path.Value ?? string.Empty);
        AppendHashField(currentHash, "query", request.QueryString.Value ?? string.Empty);
        AppendHashField(currentHash, "content-type", NormalizeMediaType(request.ContentType));

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

                var bodyBytes = buffer.AsSpan(0, bytesRead);
                currentHash.AppendData(bodyBytes);
                legacyBodyOnlyHash.AppendData(bodyBytes);
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

        var current = CurrentPrefix + Convert.ToHexString(currentHash.GetHashAndReset()).ToLowerInvariant();
        var legacyBodyOnly = LegacyBodyOnlyPrefix + Convert.ToHexString(legacyBodyOnlyHash.GetHashAndReset()).ToLowerInvariant();

        return new ApiRequestHashes(current, [current, legacyBodyOnly]);
    }

    private static void AppendHashField(IncrementalHash hash, string name, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(name));
        hash.AppendData([0]);
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }

    private static string NormalizeMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        return MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            ? parsed.MediaType.Value?.ToLowerInvariant() ?? string.Empty
            : contentType.Trim().ToLowerInvariant();
    }
}
