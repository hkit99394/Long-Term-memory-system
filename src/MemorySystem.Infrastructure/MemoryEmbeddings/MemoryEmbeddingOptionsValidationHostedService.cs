using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

internal sealed class MemoryEmbeddingOptionsValidationHostedService(
    IOptions<MemoryEmbeddingOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = options.Value;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
