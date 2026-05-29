using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Retention;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MemorySystem.Worker;

public static class EphemeralEventRetentionServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEphemeralEventRetentionWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = EphemeralEventRetentionOptions.Read(configuration);
        services.AddSingleton(Options.Create(options));

        if (!options.Enabled)
        {
            return services;
        }

        services.AddMemorySystemPostgresDataSource(configuration, environment);
        services.AddSingleton<IEphemeralEventRetentionStore, PostgresEphemeralEventRetentionStore>();
        services.AddHostedService<EphemeralEventRetentionService>();

        return services;
    }
}
