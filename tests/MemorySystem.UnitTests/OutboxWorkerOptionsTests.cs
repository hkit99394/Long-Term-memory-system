using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Workers;
using MemorySystem.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace MemorySystem.UnitTests;

public sealed class OutboxWorkerOptionsTests
{
    [Fact]
    public void Read_enables_worker_by_default()
    {
        var options = OutboxWorkerOptions.Read(new ConfigurationBuilder().Build());

        Assert.True(options.Enabled);
    }

    [Fact]
    public void Read_allows_worker_to_be_explicitly_disabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OutboxWorker:Enabled"] = "false"
            })
            .Build();

        var options = OutboxWorkerOptions.Read(configuration);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void Read_rejects_handler_timeout_that_can_outlive_lease()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OutboxWorker:LeaseDuration"] = "00:01:00",
                ["OutboxWorker:HandlerTimeout"] = "00:01:00"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => OutboxWorkerOptions.Read(configuration));

        Assert.Contains("handler timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_parses_numeric_options_with_invariant_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OutboxWorker:BatchSize"] = "2",
                    ["OutboxWorker:LeaseDuration"] = "00:01:00.500",
                    ["OutboxWorker:HandlerTimeout"] = "00:00:30.250"
                })
                .Build();

            var options = OutboxWorkerOptions.Read(configuration);

            Assert.Equal(2, options.BatchSize);
            Assert.Equal(TimeSpan.FromMilliseconds(60500), options.LeaseDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(30250), options.HandlerTimeout);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Constructor_rejects_enabled_worker_without_registered_handlers()
    {
        var processor = new OutboxJobProcessor(
            new FakeOutboxJobStore(CreateJob()),
            [],
            Options.Create(new OutboxWorkerOptions()),
            NullLogger<OutboxJobProcessor>.Instance);

        var exception = Assert.Throws<InvalidOperationException>(() => new OutboxWorkerService(
            processor,
            Options.Create(new OutboxWorkerOptions { Enabled = true }),
            new FakeWorkerHeartbeatStore(),
            NullLogger<OutboxWorkerService>.Instance));

        Assert.Contains("no outbox job handlers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readiness_allows_disabled_worker_without_registered_handlers()
    {
        var exception = Record.Exception(() => OutboxWorkerReadiness.ThrowIfCannotStart(
            new OutboxWorkerOptions { Enabled = false },
            registeredHandlerCount: 0));

        Assert.Null(exception);
    }

    [Fact]
    public void Readiness_rejects_enabled_worker_without_registered_handlers()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => OutboxWorkerReadiness.ThrowIfCannotStart(
            new OutboxWorkerOptions { Enabled = true },
            registeredHandlerCount: 0));

        Assert.Contains("OutboxWorker:Enabled=false", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMemorySystemOutboxWorker_skips_runtime_services_when_disabled()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OutboxWorker:Enabled"] = "false"
        });

        builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment);

        using var provider = builder.Services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<OutboxWorkerOptions>>().Value.Enabled);
        Assert.Empty(provider.GetServices<IHostedService>());
        Assert.Empty(provider.GetServices<IOutboxJobHandler>());
        Assert.Null(provider.GetService<OutboxJobProcessor>());
    }

    [Fact]
    public void AddMemorySystemOutboxWorker_registers_enabled_worker_composition()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["OutboxWorker:WorkerId"] = "composition-test-worker"
        });

        builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment);

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Equal("composition-test-worker", provider.GetRequiredService<IOptions<OutboxWorkerOptions>>().Value.WorkerId);
        Assert.IsType<PostgresOutboxJobStore>(provider.GetRequiredService<IOutboxJobStore>());
        Assert.IsType<PostgresWorkerHeartbeatStore>(provider.GetRequiredService<IWorkerHeartbeatStore>());
        Assert.NotEmpty(provider.GetServices<IOutboxJobHandler>());
        Assert.NotNull(provider.GetRequiredService<OutboxJobProcessor>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is OutboxWorkerService);
    }

    [Fact]
    public void AddMemorySystemOutboxWorker_rejects_enabled_deterministic_embeddings_outside_development_and_testing()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Staging"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["OutboxWorker:WorkerId"] = "composition-test-worker"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment));

        Assert.Contains("production embedding provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMemorySystemOutboxWorker_allows_configured_production_embedding_provider_outside_development_and_testing()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Staging"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
            ["Embeddings:ApiKey"] = "production-openai-key-0123456789abcdef",
            ["OutboxWorker:WorkerId"] = "composition-test-worker"
        });

        builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment);

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Equal(MemoryEmbeddingOptions.OpenAiProvider, provider.GetRequiredService<IOptions<MemoryEmbeddingOptions>>().Value.Provider);
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is OutboxWorkerService);
    }

    [Fact]
    public void AddMemorySystemOutboxWorker_rejects_placeholder_openai_key_outside_development_and_testing()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Staging"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
            ["Embeddings:ApiKey"] = "test-openai-key",
            ["OutboxWorker:WorkerId"] = "composition-test-worker"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment));

        Assert.Contains("production-safe OpenAI API key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAvailableAsync_cancels_handler_before_lease_can_expire()
    {
        var job = CreateJob();
        var store = new FakeOutboxJobStore(job);
        var handler = new SlowOutboxJobHandler();
        var processor = new OutboxJobProcessor(
            store,
            [handler],
            Options.Create(new OutboxWorkerOptions
            {
                WorkerId = "worker",
                BatchSize = 1,
                MaxAttempts = 2,
                LeaseDuration = TimeSpan.FromSeconds(1),
                HandlerTimeout = TimeSpan.FromMilliseconds(10),
                RetryDelay = TimeSpan.Zero
            }),
            NullLogger<OutboxJobProcessor>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        Assert.Equal(1, await processor.ProcessAvailableAsync(timeout.Token));
        Assert.True(handler.WasCanceled);
        Assert.Equal(job.Id, store.FailedJob?.Id);
        Assert.False(store.DeadLetter);
        Assert.Null(store.CompletedJob);
    }

    [Fact]
    public async Task ProcessAvailableAsync_dead_letters_over_budget_jobs_without_invoking_handler()
    {
        var job = CreateJob(attempts: 3);
        var store = new FakeOutboxJobStore(job);
        var handler = new CountingOutboxJobHandler();
        var processor = new OutboxJobProcessor(
            store,
            [handler],
            Options.Create(new OutboxWorkerOptions
            {
                WorkerId = "worker",
                BatchSize = 1,
                MaxAttempts = 2,
                LeaseDuration = TimeSpan.FromSeconds(30),
                HandlerTimeout = TimeSpan.FromSeconds(10),
                RetryDelay = TimeSpan.Zero
            }),
            NullLogger<OutboxJobProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessAvailableAsync());
        Assert.False(handler.WasInvoked);
        Assert.Equal(job.Id, store.FailedJob?.Id);
        Assert.True(store.DeadLetter);
        Assert.Null(store.CompletedJob);
        Assert.Contains("max attempts", store.FailureError, StringComparison.OrdinalIgnoreCase);
    }

    private static OutboxJob CreateJob(int attempts = 1)
    {
        return new OutboxJob(
            Guid.NewGuid(),
            "test.slow",
            "test",
            Guid.NewGuid(),
            "test-key",
            "{}",
            "processing",
            attempts,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(1),
            "worker",
            LastError: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeOutboxJobStore(OutboxJob job) : IOutboxJobStore
    {
        public OutboxJob? CompletedJob { get; private set; }

        public OutboxJob? FailedJob { get; private set; }

        public bool DeadLetter { get; private set; }

        public string FailureError { get; private set; } = string.Empty;

        public Task<IReadOnlyList<OutboxJob>> LeaseAvailableAsync(
            string workerId,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<OutboxJob>>([job]);
        }

        public Task<bool> CompleteAsync(OutboxJob job, CancellationToken cancellationToken = default)
        {
            CompletedJob = job;

            return Task.FromResult(true);
        }

        public Task<bool> MarkFailedAsync(
            OutboxJob job,
            string error,
            bool deadLetter,
            DateTimeOffset? availableAt,
            CancellationToken cancellationToken = default)
        {
            FailedJob = job;
            DeadLetter = deadLetter;
            FailureError = error;

            return Task.FromResult(true);
        }
    }

    private sealed class FakeWorkerHeartbeatStore : IWorkerHeartbeatStore
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
            return Task.FromResult<WorkerHeartbeatSnapshot?>(null);
        }
    }

    private sealed class SlowOutboxJobHandler : IOutboxJobHandler
    {
        public bool WasCanceled { get; private set; }

        public bool CanHandle(string jobType)
        {
            return jobType == "test.slow";
        }

        public async Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            finally
            {
                WasCanceled = cancellationToken.IsCancellationRequested;
            }
        }
    }

    private sealed class CountingOutboxJobHandler : IOutboxJobHandler
    {
        public bool WasInvoked { get; private set; }

        public bool CanHandle(string jobType)
        {
            return jobType == "test.slow";
        }

        public Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
        {
            WasInvoked = true;

            return Task.CompletedTask;
        }
    }
}
