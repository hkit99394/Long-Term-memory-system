using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public static class OutboxWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemOutboxWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = OutboxWorkerOptions.Read(configuration);
        services.AddSingleton(Options.Create(options));

        if (!options.Enabled)
        {
            return services;
        }

        services.AddMemorySystemPostgresDataSource(configuration, environment);
        services.AddSingleton<IOutboxJobStore, PostgresOutboxJobStore>();
        services.AddMemorySystemOutboxJobHandler<MemoryIndexOutboxJobHandler>();
        services.AddSingleton<OutboxJobProcessor>();
        services.AddHostedService<OutboxWorkerService>();

        return services;
    }

    public static IServiceCollection AddMemorySystemOutboxJobHandler<THandler>(this IServiceCollection services)
        where THandler : class, IOutboxJobHandler
    {
        services.AddSingleton<IOutboxJobHandler, THandler>();

        return services;
    }
}
