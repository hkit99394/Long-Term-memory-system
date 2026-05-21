using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.MemoryProposals;
using Npgsql;

namespace MemorySystem.Api.MemoryProposals;

public static class ApiMemoryProposalServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryProposals(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IMemoryProposalBroker, MinimalMemoryProposalBroker>();
        services.AddSingleton<IMemoryProposalWorkflow, MemoryProposalWorkflow>();
        services.AddSingleton<IMemoryProposalWriteStore>(serviceProvider =>
            new PostgresMemoryProposalWriteStore(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
