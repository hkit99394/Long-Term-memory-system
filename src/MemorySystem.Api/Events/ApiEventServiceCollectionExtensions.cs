using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;

namespace MemorySystem.Api.Events;

public static class ApiEventServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<PostgresEventStore>();
        services.AddSingleton<IEventAppendWorkflow, EventAppendWorkflow>();
        services.AddSingleton<IEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());
        services.AddSingleton<ISourceEventReferenceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());

        return services;
    }
}
