using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

internal sealed class MemoryEmbeddingOptionsValidationHostedService(
    IOptions<MemoryEmbeddingOptions> options,
    IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var embeddingOptions = options.Value;
        MemoryEmbeddingEnvironmentPolicy.ThrowIfProductionCredentialIsNotSafe(
            environment,
            embeddingOptions,
            "Embedding provider");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
