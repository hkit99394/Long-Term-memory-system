using MemorySystem.Infrastructure.Health;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.UnitTests;

public sealed class OperationalHealthCheckTests
{
    [Fact]
    public async Task EmbeddingProviderHealthCheck_allows_deterministic_provider_in_development()
    {
        var check = new EmbeddingProviderHealthCheck(
            new TestHostEnvironment(Environments.Development),
            new MemoryEmbeddingOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(MemoryEmbeddingOptions.DeterministicProvider, result.Data["provider"]);
    }

    [Fact]
    public async Task EmbeddingProviderHealthCheck_rejects_deterministic_provider_outside_development_and_testing()
    {
        var check = new EmbeddingProviderHealthCheck(
            new TestHostEnvironment("Staging"),
            new MemoryEmbeddingOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not usable", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbeddingProviderHealthCheck_allows_configured_openai_provider_outside_development_and_testing()
    {
        var check = new EmbeddingProviderHealthCheck(
            new TestHostEnvironment("Staging"),
            new MemoryEmbeddingOptions
            {
                Provider = MemoryEmbeddingOptions.OpenAiProvider,
                Model = MemoryEmbeddingOptions.DefaultOpenAiModel,
                Dimension = MemoryEmbeddingOptions.DefaultOpenAiDimension,
                ApiKey = "test-openai-key"
            });

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(MemoryEmbeddingOptions.OpenAiProvider, result.Data["provider"]);
    }

    [Fact]
    public async Task WorkerHeartbeatHealthCheck_reports_degraded_when_no_heartbeat_exists()
    {
        var check = new WorkerHeartbeatHealthCheck(
            new FakeWorkerHeartbeatStore(null),
            new WorkerHeartbeatHealthOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("No outbox worker heartbeat", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkerHeartbeatHealthCheck_reports_healthy_when_running_heartbeat_is_fresh()
    {
        var heartbeat = new WorkerHeartbeatSnapshot(
            WorkerHeartbeatTypes.Outbox,
            "worker-one",
            WorkerHeartbeatStatuses.Running,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            LastError: null);
        var check = new WorkerHeartbeatHealthCheck(
            new FakeWorkerHeartbeatStore(heartbeat),
            new WorkerHeartbeatHealthOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("worker-one", result.Data["workerId"]);
    }

    [Fact]
    public async Task WorkerHeartbeatHealthCheck_reports_degraded_when_heartbeat_is_stale()
    {
        var heartbeat = new WorkerHeartbeatSnapshot(
            WorkerHeartbeatTypes.Outbox,
            "worker-one",
            WorkerHeartbeatStatuses.Running,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            LastError: null);
        var check = new WorkerHeartbeatHealthCheck(
            new FakeWorkerHeartbeatStore(heartbeat),
            new WorkerHeartbeatHealthOptions { MaxAge = TimeSpan.FromMinutes(2) });

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("stale", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkerHeartbeatHealthCheck_reports_degraded_when_latest_status_is_error()
    {
        var heartbeat = new WorkerHeartbeatSnapshot(
            WorkerHeartbeatTypes.Outbox,
            "worker-one",
            WorkerHeartbeatStatuses.Error,
            DateTimeOffset.UtcNow,
            LastSuccessAt: null,
            LastError: "database unavailable");
        var check = new WorkerHeartbeatHealthCheck(
            new FakeWorkerHeartbeatStore(heartbeat),
            new WorkerHeartbeatHealthOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("database unavailable", result.Data["lastError"]);
    }

    [Fact]
    public void WorkerHeartbeatHealthOptions_rejects_non_positive_max_age()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkerHeartbeatHealth:MaxAge"] = "00:00:00"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => WorkerHeartbeatHealthOptions.Read(configuration));

        Assert.Contains("max age", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeWorkerHeartbeatStore(WorkerHeartbeatSnapshot? heartbeat) : IWorkerHeartbeatStore
    {
        public Task RecordAsync(
            WorkerHeartbeatUpdate update,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<WorkerHeartbeatSnapshot?> ReadLatestAsync(
            string workerType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(heartbeat);
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MemorySystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
