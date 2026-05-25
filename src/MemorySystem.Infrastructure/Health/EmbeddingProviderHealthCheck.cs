using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.Infrastructure.Health;

public sealed class EmbeddingProviderHealthCheck(
    IHostEnvironment environment,
    MemoryEmbeddingOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["provider"] = options.Provider,
            ["model"] = options.Model,
            ["dimension"] = options.Dimension
        };

        try
        {
            options.Validate();

            if (!MemoryEmbeddingEnvironmentPolicy.HasUsableProvider(environment, options))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Embedding provider is not usable for this environment.",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Embedding provider configuration is usable.",
                data));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Embedding provider configuration is invalid.",
                exception,
                data));
        }
    }
}
