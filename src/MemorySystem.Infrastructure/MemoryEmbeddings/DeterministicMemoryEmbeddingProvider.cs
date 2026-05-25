using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.MemoryEmbeddings;
using Microsoft.Extensions.Options;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public sealed class DeterministicMemoryEmbeddingProvider : IMemoryEmbeddingProvider
{
    private readonly MemoryEmbeddingOptions options;

    public DeterministicMemoryEmbeddingProvider(IOptions<MemoryEmbeddingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
        this.options.Validate();
    }

    public string ProviderName => this.options.Provider;

    public string Model => options.Model;

    public int Dimension => options.Dimension;

    public Task<MemoryEmbeddingVector> EmbedAsync(
        MemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);

        cancellationToken.ThrowIfCancellationRequested();

        var values = new float[options.Dimension];
        var norm = 0.0d;

        for (var index = 0; index < values.Length; index++)
        {
            var value = HashToUnitRange(options.Model, request.Input, index);
            values[index] = value;
            norm += value * value;
        }

        if (norm > 0)
        {
            var scale = 1.0d / Math.Sqrt(norm);

            for (var index = 0; index < values.Length; index++)
            {
                values[index] = (float)(values[index] * scale);
            }
        }

        return Task.FromResult(new MemoryEmbeddingVector(options.Model, options.Dimension, values));
    }

    private static float HashToUnitRange(string model, string input, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{model}\n{index}\n{input}"));
        var unsigned = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, sizeof(uint)));
        var unit = unsigned / (double)uint.MaxValue;

        return (float)((unit * 2.0d) - 1.0d);
    }
}
