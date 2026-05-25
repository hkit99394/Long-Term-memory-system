using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MemorySystem.Infrastructure.Health;

public static class MemorySystemHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddMemorySystemPostgres(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "postgres",
            serviceProvider =>
            {
                return new PostgresHealthCheck(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            },
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "postgres", "ready"],
            timeout: PostgresHealthCheck.Timeout));
    }

    public static IHealthChecksBuilder AddMemorySystemOutboxBacklog(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "outbox",
            serviceProvider =>
            {
                return new OutboxBacklogHealthCheck(
                    serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                    OutboxBacklogHealthOptions.Read(serviceProvider.GetRequiredService<IConfiguration>()));
            },
            failureStatus: HealthStatus.Degraded,
            tags: ["outbox", "ready"],
            timeout: OutboxBacklogHealthCheck.Timeout));
    }

    public static IHealthChecksBuilder AddMemorySystemWorkerHeartbeat(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "worker",
            serviceProvider =>
            {
                return new WorkerHeartbeatHealthCheck(
                    new PostgresWorkerHeartbeatStore(serviceProvider.GetRequiredService<NpgsqlDataSource>()),
                    WorkerHeartbeatHealthOptions.Read(serviceProvider.GetRequiredService<IConfiguration>()));
            },
            failureStatus: HealthStatus.Degraded,
            tags: ["worker", "ready"],
            timeout: WorkerHeartbeatHealthCheck.Timeout));
    }

    public static IHealthChecksBuilder AddMemorySystemEmbeddingProvider(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "embedding_provider",
            serviceProvider =>
            {
                return new EmbeddingProviderHealthCheck(
                    serviceProvider.GetRequiredService<IHostEnvironment>(),
                    serviceProvider.GetRequiredService<IOptions<MemoryEmbeddingOptions>>().Value);
            },
            failureStatus: HealthStatus.Unhealthy,
            tags: ["embedding", "ready"],
            timeout: TimeSpan.FromSeconds(1)));
    }
}
