using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Events;

namespace MemorySystem.Api.Events;

public static class ApiEventServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(_ =>
        {
            var connectionString = PostgresConnectionString.Resolve(
                key => configuration[key],
                configuration.GetConnectionString("Postgres"),
                environment.EnvironmentName);

            return new PostgresEventStore(connectionString);
        });
        services.AddSingleton<IEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());
        services.AddSingleton<ISourceEventReferenceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());

        return services;
    }
}
