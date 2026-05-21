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
        services.AddSingleton<IEventStore>(_ =>
        {
            var connectionString = PostgresConnectionString.Resolve(
                key => configuration[key],
                configuration.GetConnectionString("Postgres"),
                environment.EnvironmentName);

            return new PostgresEventStore(connectionString);
        });

        return services;
    }
}
