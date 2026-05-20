using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MemorySystem.UnitTests;

public sealed class OutboxWorkerOptionsTests
{
    [Fact]
    public void Read_disables_worker_by_default()
    {
        var options = OutboxWorkerOptions.Read(new ConfigurationBuilder().Build());

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
            NullLogger<OutboxWorkerService>.Instance));

        Assert.Contains("no outbox job handlers", exception.Message, StringComparison.OrdinalIgnoreCase);
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
