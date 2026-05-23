using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.MemoryProposals;

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
        services.AddSingleton<IMemoryProposalWriteStore, PostgresMemoryProposalWriteStore>();

        return services;
    }
}
