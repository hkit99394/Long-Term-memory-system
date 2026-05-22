using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;
using Npgsql;

namespace MemorySystem.Api.Events;

public static class ApiEventServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(serviceProvider =>
            new PostgresEventStore(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<IEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());
        services.AddSingleton<ISourceEventReferenceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());

        return services;
    }
}
